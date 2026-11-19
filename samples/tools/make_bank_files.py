"""Builds the sample bank files from src/SmallBooks.Data/Samples/sample-books.json.

Writes, into samples/bank/:
  - the transaction download in CSV, OFX, QFX and QBO form (October 1 to November 5, 2026)
  - the October 2026 statement as a PDF
  - answer-key.md: how the October reconciliation should come out

Run from anywhere:  python3 samples/tools/make_bank_files.py
Needs:              pip install reportlab
Everything is fictional. Re-run after changing the JSON; a test checks the CSV still matches it.
"""

from __future__ import annotations

import csv
import datetime as dt
import json
from dataclasses import dataclass
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "src" / "SmallBooks.Data" / "Samples" / "sample-books.json"
OUT = ROOT / "samples" / "bank"


@dataclass
class BankTxn:
    posted: dt.date
    type: str          # OFX TRNTYPE
    payee: str
    amount: Decimal    # + money in, - money out
    check: str | None
    fitid: str = ""
    in_books: bool = True
    book_date: dt.date | None = None
    book_memo: str | None = None
    book_amount: Decimal | None = None
    note: str | None = None


def d(s: str) -> dt.date:
    return dt.date.fromisoformat(s)


def money(x: Decimal) -> str:
    return f"{x:,.2f}"


def load():
    data = json.loads(DATA.read_text(encoding="utf-8"))
    bank = data["bank"]
    ledger = bank["ledgerAccount"]

    def net(lines):
        return sum((Decimal(l.get("dr", "0")) - Decimal(l.get("cr", "0")) for l in lines if l["a"] == ledger), Decimal(0))

    opening = net(data["opening"]["lines"])
    book = []   # (date, memo, amount) for every books entry touching the bank account
    txns: list[BankTxn] = []
    for e in data["entries"]:
        amt = net(e["lines"])
        if amt == 0:
            continue
        book.append((d(e["date"]), e["memo"], amt))
        b = e.get("bank")
        if b is None:
            raise SystemExit(f"Entry '{e['memo']}' touches the bank account but has no bank side")
        txns.append(BankTxn(d(b["posted"]), b["type"], b["payee"], Decimal(b["amount"]) if "amount" in b else amt,
                            b.get("check"), book_date=d(e["date"]), book_memo=e["memo"], book_amount=amt, note=b.get("note")))
    for b in data["bankOnly"]:
        txns.append(BankTxn(d(b["posted"]), b["type"], b["payee"], Decimal(b["amount"]), None, in_books=False, note=b.get("note")))

    # Banks list credits before debits within a day; keep it stable and give each a fixed id.
    txns.sort(key=lambda t: (t.posted, t.amount < 0, t.payee))
    for i, t in enumerate(txns, 1):
        t.fitid = f"SVB{t.posted:%Y%m%d}{i:04d}"
    return data, bank, opening, book, txns


def window(txns, period):
    a, b = d(period["from"]), d(period["to"])
    return [t for t in txns if a <= t.posted <= b]


def write_csv(path: Path, txns, opening):
    bal = opening
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["Posting Date", "Transaction ID", "Description", "Check Number", "Type", "Amount", "Balance"])
        for t in txns:
            bal += t.amount
            w.writerow([t.posted.strftime("%m/%d/%Y"), t.fitid, t.payee, t.check or "", t.type, f"{t.amount:.2f}", f"{bal:.2f}"])


def write_ofx(path: Path, bank, txns, start, end, opening, intuit: bool):
    """OFX 1.0.2 (SGML), the form most banks still hand out. QFX and QBO are the same with an Intuit id."""
    now = "20261105120000"
    ledger = opening + sum((t.amount for t in txns), Decimal(0))
    lines = [
        "OFXHEADER:100", "DATA:OFXSGML", "VERSION:102", "SECURITY:NONE", "ENCODING:USASCII",
        "CHARSET:1252", "COMPRESSION:NONE", "OLDFILEUID:NONE", "NEWFILEUID:NONE", "",
        "<OFX>",
        "<SIGNONMSGSRSV1><SONRS>",
        "<STATUS><CODE>0<SEVERITY>INFO</STATUS>",
        f"<DTSERVER>{now}", "<LANGUAGE>ENG",
        "<FI><ORG>Sample Valley Bank<FID>99999</FI>",
    ]
    if intuit:
        lines.append("<INTU.BID>99999")
    lines += [
        "</SONRS></SIGNONMSGSRSV1>",
        "<BANKMSGSRSV1><STMTTRNRS>",
        "<TRNUID>1",
        "<STATUS><CODE>0<SEVERITY>INFO</STATUS>",
        "<STMTRS>",
        "<CURDEF>USD",
        f"<BANKACCTFROM><BANKID>{bank['routing']}<ACCTID>{bank['accountNumber']}<ACCTTYPE>{bank['accountType']}</BANKACCTFROM>",
        f"<BANKTRANLIST><DTSTART>{start:%Y%m%d}<DTEND>{end:%Y%m%d}",
    ]
    for t in txns:
        lines.append("<STMTTRN>")
        lines.append(f"<TRNTYPE>{t.type}")
        lines.append(f"<DTPOSTED>{t.posted:%Y%m%d}120000")
        lines.append(f"<TRNAMT>{t.amount:.2f}")
        lines.append(f"<FITID>{t.fitid}")
        if t.check:
            lines.append(f"<CHECKNUM>{t.check}")
        lines.append(f"<NAME>{t.payee[:32]}")
        if len(t.payee) > 32:
            lines.append(f"<MEMO>{t.payee}")
        lines.append("</STMTTRN>")
    lines += [
        "</BANKTRANLIST>",
        f"<LEDGERBAL><BALAMT>{ledger:.2f}<DTASOF>{end:%Y%m%d}120000</LEDGERBAL>",
        f"<AVAILBAL><BALAMT>{ledger:.2f}<DTASOF>{end:%Y%m%d}120000</AVAILBAL>",
        "</STMTRS></STMTTRNRS></BANKMSGSRSV1>",
        "</OFX>",
    ]
    path.write_text("\r\n".join(lines) + "\r\n", encoding="ascii")


def write_pdf(path: Path, bank, opening, txns, period):
    from reportlab.lib import colors
    from reportlab.lib.pagesizes import letter
    from reportlab.lib.styles import ParagraphStyle
    from reportlab.lib.units import inch
    from reportlab.platypus import (KeepTogether, PageBreak, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle)

    start, end = d(period["from"]), d(period["to"])
    ink = colors.HexColor("#1f2a37")
    teal = colors.HexColor("#0f6e6e")
    rule = colors.HexColor("#c9d1d9")
    shade = colors.HexColor("#eef4f4")

    base = ParagraphStyle("b", fontName="Helvetica", fontSize=9, leading=12, textColor=ink)
    small = ParagraphStyle("s", parent=base, fontSize=7.5, leading=10, textColor=colors.HexColor("#4b5563"))
    h2 = ParagraphStyle("h2", parent=base, fontName="Helvetica-Bold", fontSize=11, leading=14, textColor=teal, spaceBefore=10, spaceAfter=4)

    credits = [t for t in txns if t.amount > 0]
    checks = sorted([t for t in txns if t.type == "CHECK"], key=lambda t: int(t.check))
    fees = [t for t in txns if t.type == "FEE"]
    other = [t for t in txns if t.amount < 0 and t.type not in ("CHECK", "FEE")]
    total = lambda xs: sum((t.amount for t in xs), Decimal(0))
    ending = opening + total(txns)

    def watermark(c, doc):
        c.saveState()
        c.setFont("Helvetica-Bold", 40)
        c.setFillColor(colors.Color(0.85, 0.15, 0.15, alpha=0.10))
        c.translate(letter[0] / 2, letter[1] / 2)
        c.rotate(45)
        c.drawCentredString(0, 0, "SAMPLE - NOT A REAL STATEMENT")
        c.restoreState()
        c.setFont("Helvetica", 7.5)
        c.setFillColor(colors.HexColor("#6b7280"))
        c.drawString(0.75 * inch, 0.5 * inch, f"{bank['name']} - account ending {bank['accountNumber'][-4:]} - statement period {start:%m/%d/%Y} to {end:%m/%d/%Y}")
        c.drawRightString(letter[0] - 0.75 * inch, 0.5 * inch, f"Page {doc.page}")

    def table(rows, widths, money_cols, total_row=False, header=True):
        t = Table(rows, colWidths=widths, repeatRows=1 if header else 0)
        style = [
            ("FONT", (0, 0), (-1, -1), "Helvetica", 8.5),
            ("TEXTCOLOR", (0, 0), (-1, -1), ink),
            ("LINEBELOW", (0, 0), (-1, -1), 0.25, rule),
            ("TOPPADDING", (0, 0), (-1, -1), 3), ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
        ]
        if header:
            style += [("FONT", (0, 0), (-1, 0), "Helvetica-Bold", 8.5), ("BACKGROUND", (0, 0), (-1, 0), shade)]
        for c in money_cols:
            style.append(("ALIGN", (c, 0), (c, -1), "RIGHT"))
        if total_row:
            style += [("FONT", (0, -1), (-1, -1), "Helvetica-Bold", 8.5), ("LINEABOVE", (0, -1), (-1, -1), 0.75, ink)]
        t.setStyle(TableStyle(style))
        return t

    story = []
    head = Table([
        [Paragraph(f"<font size=16 color='#0f6e6e'><b>{bank['name']}</b></font><br/>{bank['tagline']}", base),
         Paragraph(f"<b>Business Checking Statement</b><br/>{start:%B %d, %Y} through {end:%B %d, %Y}<br/>"
                   f"Account number: XXXXXX{bank['accountNumber'][-4:]}", ParagraphStyle("r", parent=base, alignment=2))],
    ], colWidths=[3.6 * inch, 3.4 * inch])
    head.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "TOP"), ("LINEBELOW", (0, 0), (-1, 0), 1.5, teal), ("BOTTOMPADDING", (0, 0), (-1, -1), 8)]))
    story += [head, Spacer(1, 14)]

    addr = Table([
        [Paragraph("<br/>".join(bank["customer"]), base),
         Paragraph("<b>Questions?</b><br/>This is made-up test data for the SmallBooks app.<br/>"
                   + "<br/>".join(bank["address"]), small)],
    ], colWidths=[3.6 * inch, 3.4 * inch])
    addr.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "TOP")]))
    story += [addr, Spacer(1, 10)]

    story.append(Paragraph("Account summary", h2))
    summary = [
        ["", "Items", "Amount"],
        [f"Beginning balance on {start - dt.timedelta(days=1):%m/%d/%Y}", "", money(opening)],
        ["Deposits and other credits", str(len(credits)), "+" + money(total(credits))],
        ["Checks paid", str(len(checks)), "-" + money(-total(checks))],
        ["Card purchases and other withdrawals", str(len(other)), "-" + money(-total(other))],
        ["Service fees", str(len(fees)), "-" + money(-total(fees))],
        [f"Ending balance on {end:%m/%d/%Y}", "", money(ending)],
    ]
    story.append(table(summary, [4.4 * inch, 0.8 * inch, 1.8 * inch], [1, 2], total_row=True))

    story.append(Paragraph("Deposits and other credits", h2))
    rows = [["Date", "Description", "Amount"]] + [[f"{t.posted:%m/%d}", t.payee, money(t.amount)] for t in credits]
    rows.append(["", "Total deposits and other credits", money(total(credits))])
    story.append(table(rows, [0.8 * inch, 4.4 * inch, 1.8 * inch], [2], total_row=True))

    story.append(Paragraph("Checks paid", h2))
    rows = [["Check", "Date paid", "Amount"]]
    prev = None
    for t in checks:
        n = int(t.check)
        mark = "*" if prev is not None and n != prev + 1 else ""
        rows.append([f"{t.check}{mark}", f"{t.posted:%m/%d}", money(-t.amount)])
        prev = n
    rows.append(["", "Total checks paid", money(-total(checks))])
    story.append(table(rows, [1.2 * inch, 4.0 * inch, 1.8 * inch], [2], total_row=True))
    story.append(Paragraph("* There is a gap in the check sequence before this number.", small))

    story.append(Paragraph("Card purchases and other withdrawals", h2))
    rows = [["Date", "Description", "Amount"]] + [[f"{t.posted:%m/%d}", t.payee, money(-t.amount)] for t in other]
    rows.append(["", "Total card purchases and other withdrawals", money(-total(other))])
    story.append(table(rows, [0.8 * inch, 4.4 * inch, 1.8 * inch], [2], total_row=True))

    story.append(Paragraph("Service fees", h2))
    rows = [["Date", "Description", "Amount"]] + [[f"{t.posted:%m/%d}", t.payee, money(-t.amount)] for t in fees]
    story.append(table(rows, [0.8 * inch, 4.4 * inch, 1.8 * inch], [2]))

    daily = {}
    bal = opening
    for t in txns:
        bal += t.amount
        daily[t.posted] = bal
    items = [[f"{k:%m/%d}", money(v)] for k, v in sorted(daily.items())]
    half = (len(items) + 1) // 2
    left, right = items[:half], items[half:] + [["", ""]] * (half - len(items[half:]))
    rows = [["Date", "Balance", "", "Date", "Balance"]] + [l + [""] + r for l, r in zip(left, right)]
    story.append(KeepTogether([Paragraph("Daily balance", h2),
                               table(rows, [0.8 * inch, 1.6 * inch, 1.2 * inch, 0.8 * inch, 1.6 * inch], [1, 4])]))

    # The classic worksheet on the back of a statement, left blank to fill in.
    story.append(PageBreak())
    story.append(Paragraph("Balancing your account", h2))
    story.append(Paragraph("Use this worksheet to check your register against this statement, the way banks print it on the back. "
                           "SmallBooks will do this for you once reconciliation is built.", base))
    story.append(Spacer(1, 8))
    blank = "_" * 14
    ws = [
        ["1. Ending balance shown on this statement", money(ending)],
        ["2. Add deposits you made that are not on this statement", "+ " + blank],
        ["3. Subtotal (1 + 2)", blank],
        ["4. Subtract checks and withdrawals you made that are not on this statement", "- " + blank],
        ["5. Adjusted bank balance (3 - 4)", blank],
        ["", ""],
        ["6. Balance in your register (books) on the statement date", blank],
        ["7. Add interest and credits on this statement not in your register", "+ " + blank],
        ["8. Subtract fees and charges on this statement not in your register", "- " + blank],
        ["9. Correct any amounts entered wrongly in your register", "+/- " + blank],
        ["10. Adjusted register balance (should equal line 5)", blank],
    ]
    story.append(table(ws, [5.2 * inch, 1.8 * inch], [1], header=False))
    story.append(Spacer(1, 12))
    out = [["Outstanding items", "Amount"]] + [["", ""] for _ in range(8)]
    story.append(table(out, [5.2 * inch, 1.8 * inch], [1]))

    doc = SimpleDocTemplate(str(path), pagesize=letter, leftMargin=0.75 * inch, rightMargin=0.75 * inch,
                            topMargin=0.7 * inch, bottomMargin=0.8 * inch,
                            title=f"{bank['name']} statement {start:%B %Y} (sample)", author="SmallBooks sample data")
    doc.build(story, onFirstPage=watermark, onLaterPages=watermark)


def write_answer_key(path: Path, data, bank, opening, book, txns):
    start, end = d(bank["statement"]["from"]), d(bank["statement"]["to"])
    stmt = window(txns, bank["statement"])
    ending = opening + sum((t.amount for t in stmt), Decimal(0))
    book_bal = opening + sum((a for (dd, _, a) in book if dd <= end), Decimal(0))

    in_transit = [t for t in txns if t.in_books and t.book_date <= end < t.posted and t.amount > 0]
    outstanding = [t for t in txns if t.in_books and t.book_date <= end < t.posted and t.amount < 0]
    bank_only = [t for t in stmt if not t.in_books]
    wrong = [t for t in stmt if t.in_books and t.amount != t.book_amount]
    adj_bank = ending + sum((t.amount for t in in_transit), Decimal(0)) + sum((t.amount for t in outstanding), Decimal(0))
    adj_book = book_bal + sum((t.amount for t in bank_only), Decimal(0)) + sum((t.amount - t.book_amount for t in wrong), Decimal(0))
    assert adj_bank == adj_book, (adj_bank, adj_book)

    def row(t, amt=None):
        a = t.amount if amt is None else amt
        return f"| {t.posted:%m/%d} | {t.payee} | {'+' if a > 0 else '-'}{money(abs(a))} | {t.note or ''} |"

    dl = window(txns, bank["download"])
    L = [
        "# October 2026 reconciliation: answer key",
        "",
        f"Made by `samples/tools/make_bank_files.py` from `sample-books.json`. Account: {bank['name']} checking "
        f"ending {bank['accountNumber'][-4:]}, which is **1000 Checking** in the sample company.",
        "",
        "## The two balances",
        "",
        "| | Amount |",
        "| --- | ---: |",
        f"| Beginning balance on the statement (9/30), same as the books' opening balance | {money(opening)} |",
        f"| **Ending balance on the statement (10/31)** | **{money(ending)}** |",
        f"| Checking balance in the books on 10/31, before reconciling | {money(book_bal)} |",
        f"| Difference to explain | {money(ending - book_bal)} |",
        "",
        "## What explains the difference",
        "",
        "Timing (no change to the books, these clear in November):",
        "",
        "| Bank date | Item | Amount | Why |",
        "| --- | --- | ---: | --- |",
        *[row(t) for t in in_transit + outstanding],
        "",
        "On the statement but not in the books (add these to the books while reconciling):",
        "",
        "| Bank date | Item | Amount | Note |",
        "| --- | --- | ---: | --- |",
        *[row(t) for t in bank_only],
        "",
        "Entered wrong in the books (fix with a correcting entry):",
        "",
        "| Bank date | Item | Off by | Note |",
        "| --- | --- | ---: | --- |",
        *[row(t, t.amount - t.book_amount) for t in wrong],
        "",
        "## Proof",
        "",
        "| | Amount |",
        "| --- | ---: |",
        f"| Statement ending balance | {money(ending)} |",
        f"| + deposits in transit | {money(sum((t.amount for t in in_transit), Decimal(0)))} |",
        f"| - outstanding checks | {money(-sum((t.amount for t in outstanding), Decimal(0)))} |",
        f"| **Adjusted bank balance** | **{money(adj_bank)}** |",
        f"| Books before reconciling | {money(book_bal)} |",
        f"| + / - items only the bank had | {money(sum((t.amount for t in bank_only), Decimal(0)))} |",
        f"| + / - corrections | {money(sum((t.amount - t.book_amount for t in wrong), Decimal(0)))} |",
        f"| **Adjusted book balance** | **{money(adj_book)}** |",
        "",
        f"After the {len(bank_only) + len(wrong)} fixes are entered, the books' checking balance on 10/31 is {money(adj_book)}. "
        f"Clearing every October statement item leaves a cleared balance of {money(ending)}, matching the statement, "
        f"with {len(in_transit) + len(outstanding)} items left uncleared.",
        "",
        "## Matching the download file",
        "",
        f"The download covers {d(bank['download']['from']):%m/%d} to {d(bank['download']['to']):%m/%d/%Y}: {len(dl)} transactions. "
        f"{sum(1 for t in dl if t.in_books and t.amount == t.book_amount)} match a books entry exactly, "
        f"{sum(1 for t in dl if t.in_books and t.amount != t.book_amount)} matches by date and payee but not amount, "
        f"and {sum(1 for t in dl if not t.in_books)} are new. Bank dates lag the books by 0 to 7 days (checks take longest). "
        "Check 1044 is missing on purpose (think of it as voided), so the statement flags a gap in the check sequence.",
        "",
    ]
    path.write_text("\n".join(L), encoding="utf-8")


def main():
    data, bank, opening, book, txns = load()
    OUT.mkdir(parents=True, exist_ok=True)
    dl = window(txns, bank["download"])
    a, b = d(bank["download"]["from"]), d(bank["download"]["to"])
    stem = f"sample-valley-checking-{a:%Y%m%d}-{b:%Y%m%d}"
    write_csv(OUT / f"{stem}.csv", dl, opening)
    write_ofx(OUT / f"{stem}.ofx", bank, dl, a, b, opening, intuit=False)
    write_ofx(OUT / f"{stem}.qfx", bank, dl, a, b, opening, intuit=True)
    write_ofx(OUT / f"{stem}.qbo", bank, dl, a, b, opening, intuit=True)
    s = d(bank["statement"]["from"])
    write_pdf(OUT / f"sample-valley-statement-{s:%Y-%m}.pdf", bank, opening, window(txns, bank["statement"]), bank["statement"])
    write_answer_key(OUT / "answer-key.md", data, bank, opening, book, txns)
    for p in sorted(OUT.iterdir()):
        print(p.relative_to(ROOT))


if __name__ == "__main__":
    main()
