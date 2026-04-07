// Tabular Editor C# script to show column profile statistics (cardinality and blanks).
// This is the "quick" version - for full distribution stats, use VizColumnDistributions.csx.
// Vibe-coded by Ruben Van de Voorde with Claude Code.
//
// Instructions
// ------------
// 1. Save this script as a macro with a context of 'Column'
// 2. Configure a keyboard shortcut for the macro if using Tabular Editor 3
// 3. Select one or more columns from the SAME table and run the script
//
// Output columns
// --------------
// # Distinct          — distinct non-blank values per column
// % Distinct          — distinct as percentage of total rows
// % Distinct (Bars)   — bar chart for distinct percentage
// # Blank             — rows with blank/null values
// % Blank             — blank percentage (conditional: only shown if any blanks found)
// % Blank (Bars)      — bar chart for blank percentage (conditional)

// ----------------------------------------------------------------------------------------------------------//
// Timing infrastructure

var _stopwatch = System.Diagnostics.Stopwatch.StartNew();

string FormatTiming()
{
    return $"({_stopwatch.ElapsedMilliseconds / 1000.0:F2}s⏱️)";
}

// ----------------------------------------------------------------------------------------------------------//
// Helper functions

// Calculate bar length for a percentage (0-12 blocks)
// For distinct: returns length at any percentage including 100%
// For blanks: returns 0 at 0% and 100% (not meaningful)
int GetBarLength(double percentage, bool isDistinct)
{
    if (percentage <= 0)
        return 0;
    if (percentage >= 100)
        return isDistinct ? 12 : 0;

    return Math.Min(12, Math.Max(1, (int)Math.Round(percentage / 100.0 * 12)));
}

// Generate percentage bar string from length
string GeneratePercentageBar(int barLength)
{
    return barLength > 0 ? new string('\u2588', barLength) : "";
}

// ----------------------------------------------------------------------------------------------------------//
// Main script execution

int _NrColumns = Selected.Columns.Count();

if (_NrColumns == 0)
{
    Info("Please select one or more columns to analyze.");
}
else
{
    // Check all columns are from the same table
    var _FirstTable = Selected.Columns.First().Table;
    bool _SameTable = Selected.Columns.All(c => c.Table == _FirstTable);

    if (!_SameTable)
    {
        Info("Please select columns from the same table only.");
    }
    else
    {
        var allColumns = Selected.Columns.ToList();
        string tableDaxName = _FirstTable.DaxObjectFullName;

        // Build DAX query for all columns
        var rows = new List<string>();
        foreach (var col in allColumns)
        {
            string colName = col.Name;
            string colDaxName = col.DaxObjectFullName;

            // # Distinct excludes blanks: subtract 1 if any blanks exist
            string rowExpr =
                "ROW(\n" +
                $@"""Column"", ""{colName}""," + "\n" +
                $@"""# Distinct"", DISTINCTCOUNT({colDaxName}) - IF(COUNTBLANK({colDaxName}) > 0, 1, 0)," + "\n" +
                $@"""# Rows"", COUNTROWS({tableDaxName})," + "\n" +
                $@"""# Blank"", COUNTBLANK({colDaxName}))";

            rows.Add(rowExpr);
        }

        string dax = rows.Count == 1
            ? rows[0]
            : "UNION(\n" + String.Join(",\n", rows) + ")";

        System.Data.DataTable result;
        try
        {
            result = EvaluateDax(dax) as System.Data.DataTable;
        }
        catch
        {
            Info("DAX query failed. Check that the selected columns are accessible.");
            return;
        }

        // Pre-scan results: check for blanks
        bool anyBlanks = false;
        if (result != null)
        {
            foreach (System.Data.DataRow row in result.Rows)
            {
                long blank = row["[# Blank]"] == DBNull.Value ? 0 : Convert.ToInt64(row["[# Blank]"]);
                if (blank > 0) { anyBlanks = true; break; }
            }
        }

        // Note: Bar column padding can vary across screen sizes, DPI and scaling
        // settings. If bars appear truncated or have excess whitespace, adjust the
        // padding values in the "Build output DataTable" section below.

        // Build output DataTable
        var outputTable = new System.Data.DataTable();
        string columnHeader = $"Column {FormatTiming()}";
        outputTable.Columns.Add(columnHeader, typeof(string));
        outputTable.Columns.Add("# Distinct", typeof(long));
        outputTable.Columns.Add("% Distinct", typeof(double));
        string distinctBarName = "% Distinct (Bars)";
        string distinctBarPadding = new string('\u00A0', 11);
        string distinctBarHeader = distinctBarName + distinctBarPadding;
        outputTable.Columns.Add(distinctBarHeader, typeof(string));
        outputTable.Columns.Add("# Blank", typeof(long));
        if (anyBlanks) outputTable.Columns.Add("% Blank", typeof(double));
        string blankBarName = "% Blank (Bars)";
        string blankBarPadding = new string('\u00A0', 15);
        string blankBarHeader = blankBarName + blankBarPadding;
        if (anyBlanks) outputTable.Columns.Add(blankBarHeader, typeof(string));

        // Populate rows
        if (result != null)
        {
            foreach (System.Data.DataRow row in result.Rows)
            {
                string colName = row["[Column]"].ToString();
                long distinct = row["[# Distinct]"] == DBNull.Value ? 0 : Convert.ToInt64(row["[# Distinct]"]);
                long totalRows = row["[# Rows]"] == DBNull.Value ? 0 : Convert.ToInt64(row["[# Rows]"]);
                long blank = row["[# Blank]"] == DBNull.Value ? 0 : Convert.ToInt64(row["[# Blank]"]);

                double pctDistinct = totalRows > 0 ? Math.Round((double)distinct / totalRows * 100, 1) : 0;
                double blankPct = totalRows > 0 ? Math.Round((double)blank / totalRows * 100, 1) : 0;
                string distinctBars = GeneratePercentageBar(GetBarLength(pctDistinct, true));
                string blankBars = GeneratePercentageBar(GetBarLength(blankPct, false));

                var newRow = outputTable.NewRow();
                newRow[columnHeader] = colName;
                newRow["# Distinct"] = distinct;
                newRow["% Distinct"] = pctDistinct;
                newRow[distinctBarHeader] = distinctBars;
                newRow["# Blank"] = blank;
                if (anyBlanks)
                {
                    newRow["% Blank"] = blankPct;
                    newRow[blankBarHeader] = blankBars;
                }

                outputTable.Rows.Add(newRow);
            }
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
