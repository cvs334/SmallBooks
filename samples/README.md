# Sample books for testing

Everything here is made up: **Riverbend Coatings (Sample)**, a small powder-coating shop, banking at
the fictional **Sample Valley Bank** (routing 999999999, account 0000123456). Use it to try the app
without touching real books, and later to try bank import and reconciliation.

## The sample company

On the start screen, press **Create the sample company**. It makes
`Documents\SmallBooks\Riverbend Coatings (Sample).sbk` and posts, through the normal ledger:

- opening balances on 9/30/2026 (checking, savings, petty cash, equipment, credit card, equipment loan),
- 29 entries from October 1 to November 4, 2026: deposits, checks 1041 to 1047, debit card buys,
  autopays, a loan payment split into principal and interest, a transfer to savings, an owner's draw,
  credit card charges and the card payment, petty cash, savings interest and depreciation.

Delete the `.sbk` file to start the sample over. The data lives in
[`src/SmallBooks.Data/Samples/sample-books.json`](../src/SmallBooks.Data/Samples/sample-books.json).

## The bank side (`bank/`)

| File | What it is |
| --- | --- |
| `sample-valley-checking-20261001-20261105.csv` | Transaction download, CSV with a running balance |
| `...ofx` / `...qfx` / `...qbo` | The same download as OFX 1.0.2, and as Quicken (QFX) and QuickBooks (QBO) Web Connect files |
| `sample-valley-statement-2026-10.pdf` | The October 2026 statement, with the balancing worksheet on the last page |
| `answer-key.md` | How the October reconciliation should come out |

The bank and the books disagree on purpose, so matching and reconciling have something to do:

- a **deposit in transit** (10/31 night deposit, credited 11/2) and an **outstanding check** (1046, cashed 11/4),
- **three items only the bank has**: an oil change bought with the debit card and never entered, a $12 service fee and $0.84 of interest,
- **one typo**: the gas autopay is $245.00 in the books but $254.00 at the bank,
- check 1044 is missing from the sequence (voided), and bank dates lag the books by up to a week.

Statement ending balance on 10/31: **$18,220.44**. Books before reconciling: $20,410.55. After the four
fixes both sides adjust to **$20,300.44**. The details are in [`bank/answer-key.md`](bank/answer-key.md).

## Changing the sample

Edit `sample-books.json`, then rebuild the bank files so they still agree:

```
pip install reportlab
python3 samples/tools/make_bank_files.py
```

A test (`SampleCompanyTests`) checks that the books post and balance, and that the CSV still matches the JSON.
