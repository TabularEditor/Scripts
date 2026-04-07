// Tabular Editor C# script to show comprehensive column statistics including distributions.
// TopN version - analyzes only the first N rows for performance on large tables.
// For full table analysis, use VizColumnDistributions.csx.
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
// # Distinct          — distinct non-blank values per column (within TopN sample)
// % Distinct          — distinct as percentage of sampled rows
// % Distinct (Bars)   — bar chart for distinct percentage
// # Blank             — rows with blank/null values (within TopN sample)
// % Blank             — blank percentage (conditional: only shown if any blanks found)
// % Blank (Bars)      — bar chart for blank percentage (conditional)
// Min                 — minimum value (alphabetical for text, True/False for boolean)
// Max                 — maximum value
// Distribution        — 12-bin Unicode sparkline histogram (numeric and datetime columns only)
// Mean                — arithmetic mean (numeric columns only)
// Median              — median value (numeric columns only)
// StdDev              — sample standard deviation (numeric columns only)

// ----------------------------------------------------------------------------------------------------------//
// Configuration

const int TopN = 10000; // Number of rows to analyze (adjust as needed)

// ----------------------------------------------------------------------------------------------------------//
// Timing infrastructure

var _stopwatch = System.Diagnostics.Stopwatch.StartNew();

string FormatTiming()
{
    return $"({_stopwatch.ElapsedMilliseconds / 1000.0:F2}s⏱️)";
}

// ----------------------------------------------------------------------------------------------------------//
// Helper functions

// Check if a column is numeric
bool IsNumericColumn(Column col)
{
    var dt = col.DataType;
    return dt == DataType.Int64 || dt == DataType.Decimal || dt == DataType.Double;
}

// Check if a column is Boolean
bool IsBooleanColumn(Column col)
{
    return col.DataType == DataType.Boolean;
}

// Check if a column is DateTime
bool IsDateTimeColumn(Column col)
{
    return col.DataType == DataType.DateTime;
}

// Block height strings (8 levels, from lowest to highest)
// Level 0 uses 3 spaces to match width of one block character
string[] HeightChars = new string[] {
    "   ",    // blank (3 spaces = ~1 block width)
    "\u2581", // ▁ level 1
    "\u2582", // ▂ level 2
    "\u2583", // ▃ level 3
    "\u2584", // ▄ level 4
    "\u2585", // ▅ level 5
    "\u2586", // ▆ level 6
    "\u2587", // ▇ level 7
    "\u2588"  // █ level 8 (full block)
};

const int BinCount = 12;

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

// Convert bin counts to block sparkline
string GenerateSparkline(List<int> binCounts)
{
    if (binCounts == null || binCounts.Count == 0)
        return "";

    int maxCount = binCounts.Max();
    if (maxCount == 0)
    {
        var emptySb = new System.Text.StringBuilder();
        for (int i = 0; i < binCounts.Count; i++)
            emptySb.Append(HeightChars[0]);
        return emptySb.ToString();
    }

    var sb = new System.Text.StringBuilder();
    foreach (int count in binCounts)
    {
        int level = (int)Math.Round((double)count / maxCount * 8);
        level = Math.Min(8, Math.Max(0, level));
        sb.Append(HeightChars[level]);
    }
    return sb.ToString();
}

// Get histogram data for a single column (TopN version)
// Returns: (min, max, binCounts, error) - error is null on success
(double min, double max, List<int> binCounts, string error) GetHistogramData(Column col, string topNTableExpr, string colNameOnly, bool isDateTime = false)
{
    string tableDaxName = col.Table.DaxObjectFullName;

    // DateTime columns: wrap with INT() to convert to OA date serial for numeric binning
    string colExpr = isDateTime ? $"INT([{colNameOnly}])" : $"[{colNameOnly}]";

    // Get min and max values from TopN subset
    string minMaxDax = $@"ROW(""Min"", MINX({topNTableExpr}, {colExpr}), ""Max"", MAXX({topNTableExpr}, {colExpr}))";

    double minVal, maxVal;
    try
    {
        var minMaxResult = EvaluateDax(minMaxDax) as System.Data.DataTable;
        if (minMaxResult == null || minMaxResult.Rows.Count == 0)
            return (0, 0, null, "MinMax query failed");

        var row = minMaxResult.Rows[0];
        if (row["[Min]"] == DBNull.Value || row["[Max]"] == DBNull.Value)
            return (0, 0, null, "All BLANK");

        minVal = Convert.ToDouble(row["[Min]"]);
        maxVal = Convert.ToDouble(row["[Max]"]);
    }
    catch (Exception ex)
    {
        return (0, 0, null, $"Error: {ex.Message}");
    }

    // Handle single-value case
    if (minVal == maxVal)
    {
        return (minVal, maxVal, new List<int> { 1 }, null);
    }

    // Build histogram bins
    double binSize = (maxVal - minVal) / BinCount;

    string dax = $@"
VAR _topN = {topNTableExpr}
VAR _binSize = {binSize.ToString(System.Globalization.CultureInfo.InvariantCulture)}
VAR _bins = GENERATESERIES({minVal.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {(maxVal - binSize).ToString(System.Globalization.CultureInfo.InvariantCulture)}, _binSize)
RETURN
ADDCOLUMNS(
    _bins,
    ""Count"",
    VAR _binStart = [Value]
    VAR _binEnd = [Value] + _binSize
    RETURN COUNTROWS(FILTER(_topN, {colExpr} >= _binStart && {colExpr} < _binEnd))
)";

    try
    {
        var result = EvaluateDax(dax) as System.Data.DataTable;
        if (result == null || result.Rows.Count == 0)
            return (minVal, maxVal, null, "Histogram query failed");

        var binCounts = new List<int>();
        foreach (System.Data.DataRow row in result.Rows)
        {
            var countVal = row["[Count]"];
            int count = (countVal == DBNull.Value) ? 0 : Convert.ToInt32(countVal);
            binCounts.Add(count);
        }

        // Fix last bin to include max value
        string lastBinDax = $@"VAR _topN = {topNTableExpr} RETURN ROW(""Count"", COUNTROWS(FILTER(_topN, {colExpr} >= {(maxVal - binSize).ToString(System.Globalization.CultureInfo.InvariantCulture)})))";
        var lastBinResult = EvaluateDax(lastBinDax) as System.Data.DataTable;
        if (lastBinResult != null && lastBinResult.Rows.Count > 0 && binCounts.Count > 0)
        {
            var lastCountVal = lastBinResult.Rows[0]["[Count]"];
            if (lastCountVal != DBNull.Value)
                binCounts[binCounts.Count - 1] = Convert.ToInt32(lastCountVal);
        }

        return (minVal, maxVal, binCounts, null);
    }
    catch (Exception ex)
    {
        return (minVal, maxVal, null, $"Error: {ex.Message}");
    }
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
        var numericColumns = allColumns.Where(c => IsNumericColumn(c)).ToList();
        var dateTimeColumns = allColumns.Where(c => IsDateTimeColumn(c)).ToList();
        var histogramColumns = numericColumns.Concat(dateTimeColumns).ToList();
        string tableDaxName = _FirstTable.DaxObjectFullName;
        string topNTableExpr = $"TOPN({TopN}, {tableDaxName})";

        // Phase 1: Universal stats for all columns
        var universalRows = new List<string>();
        foreach (var col in allColumns)
        {
            string colName = col.Name;
            string colDaxName = col.DaxObjectFullName;
            string colNameOnly = colDaxName.Split('[')[1].TrimEnd(']');

            // COUNTBLANK doesn't work on SELECTCOLUMNS, so use FILTER + ISBLANK for blanks
            string rowExpr =
                $@"VAR _topN = {topNTableExpr}
VAR _blankCount = COUNTROWS(FILTER(_topN, ISBLANK([{colNameOnly}])))
VAR _distinctCount = COUNTROWS(DISTINCT(SELECTCOLUMNS(_topN, ""_col"", [{colNameOnly}])))
RETURN ROW(
    ""Column"", ""{colName}"",
    ""# Distinct"", _distinctCount - IF(_blankCount > 0, 1, 0),
    ""# Rows"", COUNTROWS(_topN),
    ""# Blank"", _blankCount)";

            universalRows.Add(rowExpr);
        }

        string universalDax = universalRows.Count == 1
            ? universalRows[0]
            : "UNION(\n" + String.Join(",\n", universalRows) + ")";

        System.Data.DataTable universalResult;
        try
        {
            universalResult = EvaluateDax(universalDax) as System.Data.DataTable;
        }
        catch
        {
            Info("Phase 1 DAX query failed. Check that the selected columns are accessible.");
            return;
        }

        // Phase 2: Min/Max for ALL columns (numeric get numbers, text gets alphabetical, Boolean gets True/False)
        var minMaxStats = new Dictionary<string, (string min, string max)>();

        foreach (var col in allColumns)
        {
            string colName = col.Name;
            string colDaxName = col.DaxObjectFullName;
            string colNameOnly = colDaxName.Split('[')[1].TrimEnd(']');

            // Boolean columns need FORMAT() because MINX/MAXX don't support Boolean directly
            string minExpr = IsBooleanColumn(col)
                ? $@"MINX({topNTableExpr}, FORMAT([{colNameOnly}], ""True/False""))"
                : $@"MINX({topNTableExpr}, [{colNameOnly}])";
            string maxExpr = IsBooleanColumn(col)
                ? $@"MAXX({topNTableExpr}, FORMAT([{colNameOnly}], ""True/False""))"
                : $@"MAXX({topNTableExpr}, [{colNameOnly}])";

            string minMaxDax = $@"ROW(""Min"", {minExpr}, ""Max"", {maxExpr})";

            try
            {
                var minMaxResult = EvaluateDax(minMaxDax) as System.Data.DataTable;
                if (minMaxResult != null && minMaxResult.Rows.Count > 0)
                {
                    var row = minMaxResult.Rows[0];
                    string minStr = row["[Min]"] == DBNull.Value ? "" : row["[Min]"].ToString();
                    string maxStr = row["[Max]"] == DBNull.Value ? "" : row["[Max]"].ToString();
                    minMaxStats[colName] = (minStr, maxStr);
                }
            }
            catch
            {
                minMaxStats[colName] = ("", "");
            }
        }

        // Phase 3: Numeric stats (only for numeric columns)
        var numericStats = new Dictionary<string, (double mean, double median, double stdev)>();

        if (numericColumns.Count > 0)
        {
            var numericRows = new List<string>();
            foreach (var col in numericColumns)
            {
                string colName = col.Name;
                string colDaxName = col.DaxObjectFullName;
                string colNameOnly = colDaxName.Split('[')[1].TrimEnd(']');

                string rowExpr =
                    "ROW(\n" +
                    $@"""Column"", ""{colName}""," + "\n" +
                    $@"""Mean"", IFERROR(ROUND(AVERAGEX({topNTableExpr}, [{colNameOnly}]), 2), BLANK())," + "\n" +
                    $@"""Median"", IFERROR(ROUND(MEDIANX({topNTableExpr}, [{colNameOnly}]), 2), BLANK())," + "\n" +
                    $@"""StdDev"", IFERROR(ROUND(STDEVX.S({topNTableExpr}, [{colNameOnly}]), 2), BLANK()))";

                numericRows.Add(rowExpr);
            }

            string numericDax = numericRows.Count == 1
                ? numericRows[0]
                : "UNION(\n" + String.Join(",\n", numericRows) + ")";

            System.Data.DataTable numericResult = null;
            try
            {
                numericResult = EvaluateDax(numericDax) as System.Data.DataTable;
            }
            catch { }

            if (numericResult != null)
            {
                foreach (System.Data.DataRow row in numericResult.Rows)
                {
                    string colName = row["[Column]"].ToString();
                    double mean = row["[Mean]"] == DBNull.Value ? double.NaN : Convert.ToDouble(row["[Mean]"]);
                    double median = row["[Median]"] == DBNull.Value ? double.NaN : Convert.ToDouble(row["[Median]"]);
                    double stdev = row["[StdDev]"] == DBNull.Value ? double.NaN : Convert.ToDouble(row["[StdDev]"]);
                    numericStats[colName] = (mean, median, stdev);
                }
            }
        }

        // Phase 4: Histograms (numeric and datetime columns)
        var histogramData = new Dictionary<string, string>();

        foreach (var col in histogramColumns)
        {
            string colDaxName = col.DaxObjectFullName;
            string colNameOnly = colDaxName.Split('[')[1].TrimEnd(']');
            bool isDateTime = IsDateTimeColumn(col);
            var histData = GetHistogramData(col, topNTableExpr, colNameOnly, isDateTime);
            if (histData.error == null && histData.binCounts != null)
            {
                histogramData[col.Name] = GenerateSparkline(histData.binCounts);
            }
            else
            {
                histogramData[col.Name] = histData.error ?? "";
            }
        }

        // Pre-scan results: check for blanks
        bool anyBlanks = false;
        if (universalResult != null)
        {
            foreach (System.Data.DataRow row in universalResult.Rows)
            {
                long blank = row["[# Blank]"] == DBNull.Value ? 0 : Convert.ToInt64(row["[# Blank]"]);
                if (blank > 0) { anyBlanks = true; break; }
            }
        }

        // Note: Bar column padding can vary across screen sizes, DPI and scaling
        // settings. If bars appear truncated or have excess whitespace, adjust the
        // padding values below.

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
        outputTable.Columns.Add("Min", typeof(string));
        outputTable.Columns.Add("Max", typeof(string));
        string distName = "Distribution";
        string distPadding = new string('\u00A0', 18);
        string distHeader = distName + distPadding;
        outputTable.Columns.Add(distHeader, typeof(string));
        outputTable.Columns.Add("Mean", typeof(double));
        outputTable.Columns.Add("Median", typeof(double));
        outputTable.Columns.Add("StdDev", typeof(double));

        // Populate rows
        if (universalResult != null)
        {
            foreach (System.Data.DataRow row in universalResult.Rows)
            {
                string colName = row["[Column]"].ToString();
                long distinct = row["[# Distinct]"] == DBNull.Value ? 0 : Convert.ToInt64(row["[# Distinct]"]);
                long totalRows = row["[# Rows]"] == DBNull.Value ? 0 : Convert.ToInt64(row["[# Rows]"]);
                long blank = row["[# Blank]"] == DBNull.Value ? 0 : Convert.ToInt64(row["[# Blank]"]);

                double pctDistinct = totalRows > 0 ? Math.Round((double)distinct / totalRows * 100, 1) : 0;
                double blankPct = totalRows > 0 ? Math.Round((double)blank / totalRows * 100, 1) : 0;
                string distinctBars = GeneratePercentageBar(GetBarLength(pctDistinct, true));
                string blankBars = GeneratePercentageBar(GetBarLength(blankPct, false));

                bool isNumeric = numericStats.ContainsKey(colName);

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

                // Min/Max for all columns
                if (minMaxStats.ContainsKey(colName))
                {
                    newRow["Min"] = minMaxStats[colName].min;
                    newRow["Max"] = minMaxStats[colName].max;
                }

                // Distribution: numeric + datetime columns
                if (histogramData.ContainsKey(colName))
                    newRow[distHeader] = histogramData[colName];

                // Mean/Median/StdDev: numeric columns only (not meaningful for dates)
                if (isNumeric)
                {
                    var stats = numericStats[colName];
                    newRow["Mean"] = double.IsNaN(stats.mean) ? DBNull.Value : (object)stats.mean;
                    newRow["Median"] = double.IsNaN(stats.median) ? DBNull.Value : (object)stats.median;
                    newRow["StdDev"] = double.IsNaN(stats.stdev) ? DBNull.Value : (object)stats.stdev;
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
