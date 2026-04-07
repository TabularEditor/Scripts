# Data Profiling

A collection of nine Tabular Editor C# macros for exploring and profiling data in Power BI semantic models. Select columns or tables, run a macro, and get a structured data grid in TE3's output pane — one row per column with stats at a glance.

## Prerequisites

- **Tabular Editor 3** with a live connection to a Power BI or Analysis Services model
- The macros use C# features that require the **Roslyn compiler** — standard in TE3, but [TE2 users need to enable it first](https://docs.tabulareditor.com/en/how-tos/Advanced-Scripting.html#compiling-with-roslyn)

## Macros

| Macro | Context | Purpose |
|---|---|---|
| VizColumnProfiles | Column | Quick scan: cardinality and blank stats per column |
| VizColumnProfilesTopN | Column | Same, limited to first N rows (configurable) |
| VizColumnDistributions | Column | Deep scan: distinct/blank stats, min/max, Unicode histogram, mean/median/stdev |
| VizColumnDistributionsTopN | Column | Same, limited to first N rows (configurable) |
| VizSummaryData | Table / Column / Measure | High-level summary across selected tables, columns, and measures |
| VizRelationshipCoverage | Model | FK and PK coverage across all relationships, sorted by worst coverage first |
| VizStringQuality | Column | Whitespace issues (leading spaces, double spaces) and mixed types in text columns |
| VizOutliers | Column | IQR-based outlier detection for numeric columns; string length outliers for text columns |
| VizDateGaps | Column | Consecutive date spacing analysis; flags unusually large gaps |

The first six macros handle broad profiling; the last three target specific data quality dimensions.

## Installation

1. Copy the `.csx` files to your Tabular Editor 3 macros folder (`%AppData%\Local\TabularEditor3\Macros`), or use **File → New C# Script** → paste contents → **Save as Macro**
2. Set the macro context (Column, Table, or none) as noted in the table above and each script's header
3. Macros with Column context appear in the right-click menu when you select columns in TOM Explorer; optionally assign keyboard shortcuts in **Preferences → Keyboard Bindings**

## Output reference

Each macro's header comment contains the full column reference. Below is a summary for the macros with non-obvious output.

### VizColumnDistributions (and TopN variant)

| Column | What it shows | How to read it |
|---|---|---|
| # Distinct | Distinct non-blank values | High relative to row count means many unique values; low on a key column may indicate data quality issues |
| % Distinct | Distinct as % of total rows | 100% = every row is unique; 1% = highly repeated values |
| # Blank | Rows with blank/null values | Any blanks on a foreign key column mean fact rows that won't join to the dimension |
| Min / Max | Lowest and highest value | Alphabetical for text; True/False for boolean; useful for spotting impossible values |
| Distribution | 12-bin Unicode histogram (▁▂▃▄▅▆▇█) | Shape of the value distribution; a single tall bar means most values cluster in one range |
| Mean / Median / StdDev | Numeric columns only | Large gap between mean and median suggests skew or outliers |

### VizRelationshipCoverage

One row per relationship in the model, sorted by worst coverage first. Covers both directions of each relationship.

| Column | What it shows | How to read it |
|---|---|---|
| Relationship | `FactTable[FK] → DimTable[PK]` | Identifies which relationship the row describes |
| Active | Whether the relationship is active | Inactive relationships require USERELATIONSHIP() in DAX to filter |
| # FK Values | Distinct non-blank values in the FK column | — |
| # FK Unmatched | FK values with no match in the PK | These rows resolve to the blank member in the dimension; they show as BLANK in report groupings |
| % FK Coverage | (FK Values − FK Unmatched) / FK Values | 100% = every fact row links to a dimension row; anything below deserves investigation |
| # PK Values | Distinct non-blank values in the PK column | — |
| # PK Unused | PK values not referenced by any FK row | Unused dimension members inflate slicer lists; may be legitimate (inactive customers) or a data gap |
| % PK Coverage | (PK Values − PK Unused) / PK Values | Low coverage is not necessarily a problem, but worth understanding |

Note: blank FK values are not counted as unmatched — they map to the blank dimension row, which is expected behaviour.

### VizStringQuality

| Column | What it shows | How to read it |
|---|---|---|
| # Whitespace | Rows where the value differs from `TRIM(value)` | Catches leading spaces and multiple consecutive internal spaces; trailing spaces are stripped by Power BI at load time and cannot appear in the model |
| # Numeric Values | Non-blank values that parse as a number | Text columns storing numbers as strings; may cause implicit conversion issues in DAX |
| # Date Values | Non-blank values that parse as a date | Text columns storing dates as strings; relationships and time intelligence won't work on these |

Note: casing inconsistencies (e.g. "Shipped" vs "SHIPPED") are not detected — VertiPaq's default case-insensitive collation collapses them into a single stored value.

### VizOutliers

Uses the interquartile range (IQR) method: values outside Q1 − 1.5×IQR or Q3 + 1.5×IQR are flagged as outliers.

| Column | What it shows | How to read it |
|---|---|---|
| # Low | Values below the lower IQR bound | Numeric columns only; negative values in a quantity column often appear here |
| # High | Values above the upper IQR bound | Numeric columns only; unusually large amounts or placeholder values |
| Normal Range | Lower bound – upper bound | The IQR-defined "expected" range for this column |
| # Outliers | Total outlier count (low + high for numeric; length outliers for text) | For text columns, flags rows whose string length is outside the IQR of string lengths |
| % Outliers | Outliers as % of non-blank rows | Helps distinguish a handful of edge cases from a systematic issue |

## Further reading

For a walkthrough showing these macros in action, see the [Tabular Editor blog](https://tabulareditor.com/blog).

For sample AI Assistant conversations that build on macro output — drilling into whitespace issues, unused dimension members, and negative quantities — see [EXAMPLES.md](EXAMPLES.md).
