# October 2026 reconciliation: answer key

Made by `samples/tools/make_bank_files.py` from `sample-books.json`. Account: Sample Valley Bank checking ending 3456, which is **1000 Checking** in the sample company.

## The two balances

| | Amount |
| --- | ---: |
| Beginning balance on the statement (9/30), same as the books' opening balance | 18,452.36 |
| **Ending balance on the statement (10/31)** | **18,220.44** |
| Checking balance in the books on 10/31, before reconciling | 20,410.55 |
| Difference to explain | -2,190.11 |

## What explains the difference

Timing (no change to the books, these clear in November):

| Bank date | Item | Amount | Why |
| --- | --- | ---: | --- |
| 11/02 | NIGHT DEPOSIT | +2,640.00 | Deposit in transit: dropped in the night box October 31, credited November 2. |
| 11/04 | CHECK 1046 | -560.00 | Outstanding check: written in October, cashed in November. |

On the statement but not in the books (add these to the books while reconciling):

| Bank date | Item | Amount | Note |
| --- | --- | ---: | --- |
| 10/19 | QUICK LUBE EXPRESS ANYTOWN ZZ | -89.95 | Oil change on the shop truck; the receipt never made it into the books. |
| 10/31 | INTEREST PAID | +0.84 | Checking interest, only seen on the statement. |
| 10/31 | MONTHLY SERVICE FEE | -12.00 | Bank fee, only seen on the statement. |

Entered wrong in the books (fix with a correcting entry):

| Bank date | Item | Off by | Note |
| --- | --- | ---: | --- |
| 10/21 | RIVERBEND NATURAL GAS AUTOPAY | -9.00 | Typo on purpose: the books say 245.00 but the bank took 254.00, a 9.00 difference. |

## Proof

| | Amount |
| --- | ---: |
| Statement ending balance | 18,220.44 |
| + deposits in transit | 2,640.00 |
| - outstanding checks | 560.00 |
| **Adjusted bank balance** | **20,300.44** |
| Books before reconciling | 20,410.55 |
| + / - items only the bank had | -101.11 |
| + / - corrections | -9.00 |
| **Adjusted book balance** | **20,300.44** |

After the 4 fixes are entered, the books' checking balance on 10/31 is 20,300.44. Clearing every October statement item leaves a cleared balance of 18,220.44, matching the statement, with 2 items left uncleared.

## Matching the download file

The download covers 10/01 to 11/05/2026: 26 transactions. 22 match a books entry exactly, 1 matches by date and payee but not amount, and 3 are new. Bank dates lag the books by 0 to 7 days (checks take longest). Check 1044 is missing on purpose (think of it as voided), so the statement flags a gap in the check sequence.
