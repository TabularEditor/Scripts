// Tabular Editor C# script to detect string quality issues: whitespace and mixed types.
// Vibe-coded by Ruben Van de Voorde with Claude Code.
//
// Instructions
// ------------
// 1. Save this script as a macro with a context of 'Column'
// 2. Configure a keyboard shortcut for the macro if using Tabular Editor 3
// 3. Select one or more string columns from the SAME table and run the script
// 4. Non-text columns in the selection are silently skipped
//
// Output columns
// --------------
// # Whitespace     — rows where the value differs from TRIM(value): leading spaces or
//                    multiple consecutive internal spaces.
//                    NOTE: Power BI strips trailing spaces during model load, so they cannot
//                    exist in the model and are not reported here. TRIM() in DAX removes
//                    leading, trailing, and extra internal spaces — but since trailing spaces
//                    are already gone, this check only surfaces leading or multiple internal spaces.
//                    Ref: https://learn.microsoft.com/en-us/power-bi/connect-data/desktop-data-types#leading-and-trailing-spaces
// # Date Values    — non-blank values that parse as a date via DATEVALUE()
// # Numeric Values — non-blank values that parse as a number via VALUE() (excluding date hits)
// % WS / % Mixed   — row percentages shown as block bar charts (conditional: only if any issues found)
//
// Note: casing inconsistency detection (e.g. "Shipped" vs "SHIPPED") is not included because
// VertiPaq's default case-insensitive collation collapses casing variants into a single value.
// DISTINCTCOUNT cannot distinguish them, and they never surface as duplicates in reports.

// ----------------------------------------------------------------------------------------------------------//
// Timing infrastructure

var _stopwatch = System.Diagnostics.Stopwatch.StartNew();

string FormatTiming()
{
    return $"({_stopwatch.ElapsedMilliseconds / 1000.0:F2}s⏱️)";
}

// ----------------------------------------------------------------------------------------------------------//
// Helper functions

int GetBarLength(double percentage)
{
    if (percentage <= 0) return 0;
    if (percentage >= 100) return 12;
    return Math.Min(12, Math.Max(1, (int)Math.Round(percentage / 100.0 * 12)));
}

string GeneratePercentageBar(int barLength)
{
    return barLength > 0 ? new string('\u2588', barLength) : "";
}

// ----------------------------------------------------------------------------------------------------------//
// Main script execution

if (Selected.Columns.Count() == 0)
{
    Info("Please select one or more columns to analyze.");
}
else
{
    var _FirstTable = Selected.Columns.First().Table;

    if (!Selected.Columns.All(c => c.Table == _FirstTable))
    {
        Info("Please select columns from the same table only.");
    }
    else
    {
        // Only analyze string columns; silently skip others
        var stringCols = Selected.Columns.Where(c => c.DataType == DataType.String).ToList();

        if (stringCols.Count == 0)
        {
            Info("No text/string columns in selection. VizStringQuality only analyzes text columns.");
        }
        else
        {
            string tableDaxName = _FirstTable.DaxObjectFullName;

            // Per-column results
            var results = new List<(string name, long total, long whitespace, long numeric, long date)>();

            foreach (var col in stringCols)
            {
                string colName = col.Name;
                string colDax  = col.DaxObjectFullName;

                // One query per column — DAX engine parallelises separate EvaluateDax calls more
                // efficiently than a single large UNION (see DEVELOPMENT_NOTES.md, Task 5).
                string dax = $@"
VAR _total = COUNTROWS({tableDaxName})
VAR _whitespace =
    SUMX(
        FILTER({tableDaxName}, NOT ISBLANK({colDax})),
        IF({colDax} <> TRIM({colDax}), 1, 0)
    )
VAR _date =
    SUMX(
        FILTER({tableDaxName}, NOT ISBLANK({colDax})),
        IF(IFERROR(DATEVALUE({colDax}), BLANK()) <> BLANK(), 1, 0)
    )
VAR _numeric =
    SUMX(
        FILTER({tableDaxName}, NOT ISBLANK({colDax}) && IFERROR(DATEVALUE({colDax}), BLANK()) = BLANK()),
        IF(IFERROR(VALUE({colDax}) + 0, BLANK()) <> BLANK(), 1, 0)
    )
RETURN ROW(
    ""Total"",       _total,
    ""Whitespace"",  _whitespace,
    ""Numeric"",     _numeric,
    ""Date"",        _date
)";

                try
                {
                    var result = EvaluateDax(dax) as System.Data.DataTable;
                    if (result != null && result.Rows.Count > 0)
                    {
                        var r = result.Rows[0];
                        long total   = r["[Total]"]      == DBNull.Value ? 0 : Convert.ToInt64(r["[Total]"]);
                        long ws      = r["[Whitespace]"] == DBNull.Value ? 0 : Convert.ToInt64(r["[Whitespace]"]);
                        long numeric = r["[Numeric]"]    == DBNull.Value ? 0 : Convert.ToInt64(r["[Numeric]"]);
                        long date    = r["[Date]"]       == DBNull.Value ? 0 : Convert.ToInt64(r["[Date]"]);
                        results.Add((colName, total, ws, numeric, date));
                    }
                }
                catch
                {
                    // Column failed — add zeros so it still appears in output
                    results.Add((colName, 0, 0, 0, 0));
                }
            }

            // Pre-scan to determine conditional columns
            bool anyWhitespace = results.Any(r => r.whitespace > 0);
            bool anyMixed      = results.Any(r => r.numeric > 0 || r.date > 0);

            // Note: Bar column padding can vary across screen sizes, DPI and scaling
            // settings. If bars appear truncated or have excess whitespace, adjust the
            // padding values below.

            // Build output DataTable
            var outputTable = new System.Data.DataTable();

            string colHeader = $"Column {FormatTiming()}";
            outputTable.Columns.Add(colHeader, typeof(string));

            outputTable.Columns.Add("# Whitespace", typeof(long));
            string wsBarHeader = "% WS (Bars)" + new string('\u00A0', 3);
            if (anyWhitespace)
            {
                outputTable.Columns.Add("% WS", typeof(double));
                outputTable.Columns.Add(wsBarHeader, typeof(string));
            }

            outputTable.Columns.Add("# Numeric Values", typeof(long));
            outputTable.Columns.Add("# Date Values", typeof(long));
            string mixedBarHeader = "% Mixed (Bars)" + new string('\u00A0', 0);
            if (anyMixed)
            {
                outputTable.Columns.Add("% Mixed", typeof(double));
                outputTable.Columns.Add(mixedBarHeader, typeof(string));
            }

            // Populate rows
            foreach (var (name, total, whitespace, numeric, date) in results)
            {
                double pctWs    = total > 0 ? Math.Round((double)whitespace          / total * 100, 1) : 0;
                double pctMixed = total > 0 ? Math.Round((double)(numeric + date)    / total * 100, 1) : 0;

                var row = outputTable.NewRow();
                row[colHeader]          = name;
                row["# Whitespace"]     = whitespace;
                if (anyWhitespace)
                {
                    row["% WS"]         = pctWs;
                    row[wsBarHeader]    = GeneratePercentageBar(GetBarLength(pctWs));
                }
                row["# Numeric Values"] = numeric;
                row["# Date Values"]    = date;
                if (anyMixed)
                {
                    row["% Mixed"]      = pctMixed;
                    row[mixedBarHeader] = GeneratePercentageBar(GetBarLength(pctMixed));
                }

                outputTable.Rows.Add(row);
            }

            if (outputTable.Rows.Count > 0)
            {
                outputTable.Output();
            }
            else
            {
                Info("No results to display.");
            }
        }
    }
}
