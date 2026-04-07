# AI Assistant: sample conversations

The following conversations took place in Tabular Editor 3's AI Assistant against the SpaceParts dataset, following macro scans as described in the [Tabular Editor blog](https://tabulareditor.com/blog).

---

## Conversation 1: Whitespace in Customer Sold-To Name

> *"The macro VizStringQuality found whitespace issues in Customer Sold-To Name. Which distinct issues are there, and how many rows/values are affected?"*

```dax
EVALUATE
VAR Base =
    ADDCOLUMNS(
        VALUES ( Customers[Customer Sold-To Name] ),
        "HasLeading", LEFT ( Customers[Customer Sold-To Name], 1 ) = " ",
        "HasTrailing", RIGHT ( Customers[Customer Sold-To Name], 1 ) = " ",
        "HasDoubleSpace", CONTAINSSTRING ( Customers[Customer Sold-To Name], "  " ),
        "HasTab", CONTAINSSTRING ( Customers[Customer Sold-To Name], UNICHAR ( 9 ) ),
        "HasCR", CONTAINSSTRING ( Customers[Customer Sold-To Name], UNICHAR ( 13 ) ),
        "HasLF", CONTAINSSTRING ( Customers[Customer Sold-To Name], UNICHAR ( 10 ) ),
        "HasNBSP", CONTAINSSTRING ( Customers[Customer Sold-To Name], UNICHAR ( 160 ) )
    )
RETURN
    ROW(
        "Distinct values", COUNTROWS ( Base ),
        "Leading space values", COUNTROWS ( FILTER ( Base, [HasLeading] ) ),
        "Trailing space values", COUNTROWS ( FILTER ( Base, [HasTrailing] ) ),
        "Double space values", COUNTROWS ( FILTER ( Base, [HasDoubleSpace] ) ),
        "Tab values", COUNTROWS ( FILTER ( Base, [HasTab] ) ),
        "CR values", COUNTROWS ( FILTER ( Base, [HasCR] ) ),
        "LF values", COUNTROWS ( FILTER ( Base, [HasLF] ) ),
        "NBSP values", COUNTROWS ( FILTER ( Base, [HasNBSP] ) )
    )
```

The assistant checked all whitespace types: leading spaces, trailing spaces, double spaces, tabs, CR/LF, and non-breaking spaces. Only double spaces were present — 118 distinct values affected across 209 rows. No leading/trailing spaces, tabs, or non-breaking spaces.

---

> *"Print the top 5 (alphabetical) Customer Sold-To Names affected"*

```dax
EVALUATE
VAR AffectedRows =
    FILTER (
        ALL ( 'Customers'[Customer Sold-To Name] ),
        NOT ISBLANK ( 'Customers'[Customer Sold-To Name] )
            && CONTAINSSTRING ( 'Customers'[Customer Sold-To Name], "  " )
    )
RETURN
    TOPN ( 5, AffectedRows, 'Customers'[Customer Sold-To Name], ASC )
ORDER BY 'Customers'[Customer Sold-To Name] ASC
```

Top 5 affected values, alphabetically:

1. Aminu Kano Navy (Ephyra  Branch)
2. Aminu Kano Navy (Nether  Branch)
3. Andromeda Shipyards (Ephyra  Branch)
4. Antarctic Corporation  (AG Refueling & Supply Station Branch)
5. Antarctic Corporation  (Apollo Station Branch)

---

> *"It's not very clear in the UI where the double spaces are. Write a query that returns the affected values with visible symbols instead of space"*

The assistant filtered to values containing double spaces and returned each value with spaces replaced by a visible symbol (`·`), alongside a row count per affected value.

---

## Conversation 2: Unused dimension members in Customers

> *"VizRelationshipCoverage shows unmatched PK values in the Customers dimension, customers that don't appear in any invoice. How many are there, and which customers are they?"*

```dax
EVALUATE
VAR UnmatchedCustomers =
    EXCEPT(
        VALUES(Customers[Customer Key]),
        VALUES(Invoices[Customer Key])
    )
RETURN
    ADDCOLUMNS(
        UnmatchedCustomers,
        "Customer Sold-To Name", CALCULATE(SELECTEDVALUE(Customers[Customer Sold-To Name])),
        "Account Name", CALCULATE(SELECTEDVALUE(Customers[Account Name])),
        "Key Account Name", CALCULATE(SELECTEDVALUE(Customers[Key Account Name])),
        "Account Type", CALCULATE(SELECTEDVALUE(Customers[Account Type])),
        "Transaction Type", CALCULATE(SELECTEDVALUE(Customers[Transaction Type]))
    )
ORDER BY [Customer Key]
```

There are 23 customers in the Customers dimension with no matching rows in Invoices.

| Customer Key | Customer Sold-To Name | Account Type | Transaction Type |
|---|---|---|---|
| 10000188B | Supernova Spaceparts (Sol V Salvaging Station Branch) | Key Account | B2B |
| 10000290B | Elysium Aerospace (Norheim Substation Branch) | Key Account | B2B |
| 10000428B | Aminu Kano Navy (Shepard Station Branch) | Key Account | B2B |
| 10000475B | Black Flag Spaceships (Norheim Substation Branch) | Key Account | B2B |
| 10000625B | Ingo's Foundries | No Key Account | B2B |
| 10000690B | Black Flag Spaceships (Grondsgrunn Substation Branch) | Key Account | B2B |
| 10000877B | Ashley's Shipbuilders | No Key Account | B2B |
| 10000971B | Aminu Kano Navy (Port Auckney Branch) | Key Account | B2B |
| 10001299B | Henry's Trades | No Key Account | B2B |
| 10001538C | Ana Tello Vicente | Private Buyer | B2C |
| 10001626B | Sigil Interstellar (Mermerus Station Branch) | Key Account | B2B |
| 10001785B | Thesra's Reactors | No Key Account | B2B |
| 10001824C | Laure Le Hebert | Private Buyer | B2C |
| 10001929B | Lunar Groups | No Key Account | B2B |
| 10002103C | Danny Hughes | Private Buyer | B2C |
| 10002183B | Nancy's Galactic Imports | No Key Account | B2B |
| 10002232B | Galactic Rockets | No Key Account | B2B |
| 10002456B | Christopher's Shipwrights | No Key Account | B2B |
| 10002762B | Elysa's Weaponry | No Key Account | B2B |
| 10002807B | The Burning Planet | No Key Account | B2B |
| 10003037B | Piotr's Reparations | No Key Account | B2B |
| 10003154B | Aminu Kano Navy (Pallas Station Branch) | Key Account | B2B |
| 10003186B | Pioneering Systems (Enceladus Branch) | Key Account | B2B |

---

> *"Is there a pattern in these customers vs the other customers?"*

First attempt — failed. `SUMMARIZECOLUMNS` does not accept a table variable as its first argument; it requires column references from the model.

```dax
EVALUATE
VAR UnmatchedCustomers =
    FILTER(
        ADDCOLUMNS(
            VALUES(Customers[Customer Key]),
            "InvoiceCount", CALCULATE(COUNTROWS(Invoices)),
            "OrderCount", CALCULATE(COUNTROWS(Orders)),
            "BudgetCount", CALCULATE(COUNTROWS(Budget)),
            "AccountType", CALCULATE(SELECTEDVALUE(Customers[Account Type])),
            "TransactionType", CALCULATE(SELECTEDVALUE(Customers[Transaction Type])),
            "Station", CALCULATE(SELECTEDVALUE(Customers[Station])),
            "AccountName", CALCULATE(SELECTEDVALUE(Customers[Account Name])),
            "SoldToName", CALCULATE(SELECTEDVALUE(Customers[Customer Sold-To Name])),
            "KeyAccountName", CALCULATE(SELECTEDVALUE(Customers[Key Account Name]))
        ),
        [InvoiceCount] = 0
    )
RETURN
    SUMMARIZECOLUMNS(
        UnmatchedCustomers[AccountType],
        UnmatchedCustomers[TransactionType],
        "Customers", COUNTROWS(UnmatchedCustomers),
        "With Orders", COUNTROWS(FILTER(UnmatchedCustomers, [OrderCount] > 0)),
        "With Budget", COUNTROWS(FILTER(UnmatchedCustomers, [BudgetCount] > 0))
    )
```

Succeeded on retry, switching to `SELECTCOLUMNS`:

```dax
EVALUATE
VAR UnmatchedCustomers =
    FILTER(
        ADDCOLUMNS(
            VALUES(Customers[Customer Key]),
            "InvoiceCount", CALCULATE(COUNTROWS(Invoices)),
            "OrderCount", CALCULATE(COUNTROWS(Orders)),
            "BudgetCount", CALCULATE(COUNTROWS(Budget)),
            "AccountType", CALCULATE(SELECTEDVALUE(Customers[Account Type])),
            "TransactionType", CALCULATE(SELECTEDVALUE(Customers[Transaction Type])),
            "Station", CALCULATE(SELECTEDVALUE(Customers[Station])),
            "AccountName", CALCULATE(SELECTEDVALUE(Customers[Account Name])),
            "SoldToName", CALCULATE(SELECTEDVALUE(Customers[Customer Sold-To Name])),
            "KeyAccountName", CALCULATE(SELECTEDVALUE(Customers[Key Account Name]))
        ),
        [InvoiceCount] = 0
    )
RETURN
    SELECTCOLUMNS(
        UnmatchedCustomers,
        "Customer Key", Customers[Customer Key],
        "Account Type", [AccountType],
        "Transaction Type", [TransactionType],
        "Orders", [OrderCount],
        "Budget", [BudgetCount],
        "Station", [Station],
        "Account Name", [AccountName],
        "Sold-To", [SoldToName],
        "Key Account Name", [KeyAccountName]
    )
ORDER BY [Account Type], [Transaction Type], [Customer Key]
```

All 23 customers still appear in Orders — not dead dimension members, just invoice-less. They split by account type: 9 Key Account, 11 No Key Account, 3 Private Buyer. The Key Account ones are mostly branch-level customers tied to a shared parent (Aminu Kano Navy, Black Flag Spaceships) — a pattern, not random noise.

---

## Conversation 3: Negative quantities in Net Invoice Quantity

> *"VizOutliers flagged low-value outliers on Net Invoice Quantity, including negative values. Are all negative quantities on return order types, or are some on regular invoices?"*

First attempt — failed. `SUMMARIZECOLUMNS` does not accept a table variable as its first argument.

```dax
EVALUATE
VAR Negatives =
    FILTER(
        ADDCOLUMNS(
            'Invoices',
            "DocGroup", RELATED('Invoice Document Type'[Group]),
            "DocText", RELATED('Invoice Document Type'[Text])
        ),
        'Invoices'[Net Invoice Quantity] < 0
    )
RETURN
SUMMARIZECOLUMNS(
    Negatives[DocGroup],
    Negatives[DocText],
    "Rows", COUNTROWS(Negatives),
    "Qty", SUMX(Negatives, 'Invoices'[Net Invoice Quantity])
)
ORDER BY [Rows] DESC, [Qty] ASC
```

Succeeded on retry, switching to `SUMMARIZE`:

```dax
EVALUATE
VAR Negatives =
    FILTER(
        ADDCOLUMNS(
            'Invoices',
            "DocGroup", RELATED('Invoice Document Type'[Group]),
            "DocText", RELATED('Invoice Document Type'[Text])
        ),
        'Invoices'[Net Invoice Quantity] < 0
    )
RETURN
SUMMARIZE(
    Negatives,
    [DocGroup],
    [DocText],
    "Rows", COUNTROWS(Negatives),
    "Qty", SUMX(Negatives, 'Invoices'[Net Invoice Quantity])
)
ORDER BY [Rows] DESC, [Qty] ASC
```

All 1.93 million negative rows map to Invoice Document Type group **Adjustment** — specifically Warranty and Return document types. No regular invoice document types contain negative quantities. Expected business logic, not a data error — but worth confirming that measures summing quantity handle adjustment rows correctly.
