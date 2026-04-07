// Tabular Editor C# script to detect outliers using the IQR method.
// For numeric columns: values outside Q1 − 1.5×IQR or Q3 + 1.5×IQR.
// For string columns:  rows whose string length falls outside the length IQR range.
// Date, boolean, and other column types are silently skipped.
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
// [numeric columns only]
// # Low              — values below the lower IQR bound
// # High             — values above the upper IQR bound
// Normal Range       — the IQR-based normal range (lower – upper bound)
// [all columns]
// # Outliers         — total outliers (low + high for numeric; length outliers for string)
// # Outliers (Bars)  — proportional bar chart comparing outlier counts across columns
// % Outliers         — outlier percentage of non-blank rows

// ----------------------------------------------------------------------------------------------------------//
// Timing infrastructure

var _stopwatch = System.Diagnostics.Stopwatch.StartNew();

string FormatTiming()
{
    return $"({_stopwatch.ElapsedMilliseconds / 1000.0:F2}s⏱️)";
}

// ----------------------------------------------------------------------------------------------------------//
// Helper functions

string GenerateProportionalBar(long value, long maxValue)
{
    if (maxValue <= 0 || value <= 0) return "";
    int barLength = Math.Max(1, (int)Math.Round((double)value / maxValue * 12));
    return new string('\u2588', barLength);
}


// ----------------------------------------------------------------------------------------------------------//
// Main script execution

if (Selected.Columns.Count() == 0)
{
    Info("Please select one or more columns to analyze.");
}
else
{
    var firstTable = Selected.Columns.First().Table;

    if (!Selected.Columns.All(c => c.Table == firstTable))
    {
        Info("Please select columns from the same table only.");
    }
    else
    {
        var numericCols = Selected.Columns
            .Where(c => c.DataType == DataType.Double || c.DataType == DataType.Int64 || c.DataType == DataType.Decimal)
            .ToList();
        var stringCols = Selected.Columns
            .Where(c => c.DataType == DataType.String)
            .ToList();

        if (numericCols.Count == 0 && stringCols.Count == 0)
        {
            Info("No numeric or text columns in selection. VizOutliers only analyzes numeric and text columns.");
        }
        else
        {
            string tableDaxName = firstTable.DaxObjectFullName;

            // Tuple: name, nonBlank, lower bound, upper bound, lowOut, highOut, lenOut, isNumeric
            var results = new List<(string name, long nonBlank, double lower, double upper, long lowOut, long highOut, long lenOut, bool isNumeric)>();

            // Numeric columns — IQR on values
            foreach (var col in numericCols)
            {
                string colDax = col.DaxObjectFullName;
                string dax = $@"
VAR _q1      = PERCENTILE.INC({colDax}, 0.25)
VAR _q3      = PERCENTILE.INC({colDax}, 0.75)
VAR _iqr     = _q3 - _q1
VAR _lower   = _q1 - 1.5 * _iqr
VAR _upper   = _q3 + 1.5 * _iqr
VAR _nonBlank = COUNTROWS(FILTER({tableDaxName}, NOT ISBLANK({colDax})))
VAR _lowOut  = COUNTROWS(FILTER({tableDaxName}, NOT ISBLANK({colDax}) && {colDax} < _lower))
VAR _highOut = COUNTROWS(FILTER({tableDaxName}, NOT ISBLANK({colDax}) && {colDax} > _upper))
RETURN ROW(
    ""NonBlank"", _nonBlank,
    ""Lower"",    _lower,
    ""Upper"",    _upper,
    ""LowOut"",   _lowOut,
    ""HighOut"",  _highOut
)";
                try
                {
                    var result = EvaluateDax(dax) as System.Data.DataTable;
                    if (result != null && result.Rows.Count > 0)
                    {
                        var r        = result.Rows[0];
                        long nb      = r["[NonBlank]"] == DBNull.Value ? 0 : Convert.ToInt64(r["[NonBlank]"]);
                        double lower = r["[Lower]"]    == DBNull.Value ? 0 : Convert.ToDouble(r["[Lower]"]);
                        double upper = r["[Upper]"]    == DBNull.Value ? 0 : Convert.ToDouble(r["[Upper]"]);
                        long lowOut  = r["[LowOut]"]   == DBNull.Value ? 0 : Convert.ToInt64(r["[LowOut]"]);
                        long highOut = r["[HighOut]"]  == DBNull.Value ? 0 : Convert.ToInt64(r["[HighOut]"]);
                        results.Add((col.Name, nb, lower, upper, lowOut, highOut, 0, true));
                    }
                }
                catch
                {
                    results.Add((col.Name, 0, 0, 0, 0, 0, 0, true));
                }
            }

            // String columns — IQR on string length
            foreach (var col in stringCols)
            {
                string colDax = col.DaxObjectFullName;
                string dax = $@"
VAR _nonBlank  = FILTER({tableDaxName}, NOT ISBLANK({colDax}))
VAR _nonBlankCt = COUNTROWS(_nonBlank)
VAR _q1Len     = PERCENTILEX.INC(_nonBlank, LEN({colDax}), 0.25)
VAR _q3Len     = PERCENTILEX.INC(_nonBlank, LEN({colDax}), 0.75)
VAR _iqrLen    = _q3Len - _q1Len
VAR _lowerLen  = _q1Len - 1.5 * _iqrLen
VAR _upperLen  = _q3Len + 1.5 * _iqrLen
VAR _lenOut    = COUNTROWS(FILTER(_nonBlank, LEN({colDax}) < _lowerLen || LEN({colDax}) > _upperLen))
RETURN ROW(
    ""NonBlank"",  _nonBlankCt,
    ""LowerLen"",  _lowerLen,
    ""UpperLen"",  _upperLen,
    ""LenOut"",    _lenOut
)";
                try
                {
                    var result = EvaluateDax(dax) as System.Data.DataTable;
                    if (result != null && result.Rows.Count > 0)
                    {
                        var r         = result.Rows[0];
                        long nb       = r["[NonBlank]"]  == DBNull.Value ? 0 : Convert.ToInt64(r["[NonBlank]"]);
                        double lowerL = r["[LowerLen]"]  == DBNull.Value ? 0 : Convert.ToDouble(r["[LowerLen]"]);
                        double upperL = r["[UpperLen]"]  == DBNull.Value ? 0 : Convert.ToDouble(r["[UpperLen]"]);
                        long lenOut   = r["[LenOut]"]    == DBNull.Value ? 0 : Convert.ToInt64(r["[LenOut]"]);
                        results.Add((col.Name, nb, lowerL, upperL, 0, 0, lenOut, false));
                    }
                }
                catch
                {
                    results.Add((col.Name, 0, 0, 0, 0, 0, 0, false));
                }
            }

            // Pre-scan for conditional columns
            bool anyNumeric  = results.Any(r => r.isNumeric);
            bool anyOutliers = results.Any(r => (r.isNumeric ? r.lowOut + r.highOut : r.lenOut) > 0);
            long maxOutliers = results.Max(r => r.isNumeric ? r.lowOut + r.highOut : r.lenOut);

            // Note: Bar column padding can vary across screen sizes, DPI and scaling
            // settings. If bars appear truncated or have excess whitespace, adjust the
            // padding values below.

            // Build output DataTable
            var outputTable = new System.Data.DataTable();

            string colHeader = $"Column {FormatTiming()}";
            outputTable.Columns.Add(colHeader, typeof(string));

            if (anyNumeric)
            {
                outputTable.Columns.Add("# Low",  typeof(long));
                outputTable.Columns.Add("# High", typeof(long));
            }

            outputTable.Columns.Add("Normal Range", typeof(string));
            outputTable.Columns.Add("# Outliers",   typeof(long));

            string barHeader = "# Outliers (Bars)" + new string('\u00A0', 0);
            if (anyOutliers)
            {
                outputTable.Columns.Add(barHeader,      typeof(string));
                outputTable.Columns.Add("% Outliers",   typeof(double));
            }

            foreach (var (name, nonBlank, lower, upper, lowOut, highOut, lenOut, isNumeric) in results)
            {
                long   totalOut = isNumeric ? lowOut + highOut : lenOut;
                double pctOut   = nonBlank > 0 ? Math.Round((double)totalOut / nonBlank * 100, 1) : 0.0;

                // Format normal range — collapse to single value when lower ≈ upper
                string range;
                if (isNumeric)
                {
                    double lo = Math.Round(lower, 2);
                    double hi = Math.Round(upper, 2);
                    range = lo == hi ? $"{lo}" : $"{lo} – {hi}";
                }
                else
                {
                    long lo = (long)Math.Round(lower, 0);
                    long hi = (long)Math.Round(upper, 0);
                    range = lo == hi ? $"length {lo}" : $"length {lo} – {hi}";
                }

                var row = outputTable.NewRow();
                row[colHeader] = name;

                if (anyNumeric)
                {
                    row["# Low"]  = isNumeric ? (object)lowOut  : DBNull.Value;
                    row["# High"] = isNumeric ? (object)highOut : DBNull.Value;
                }

                row["Normal Range"] = range;
                row["# Outliers"]   = totalOut;

                if (anyOutliers)
                {
                    row[barHeader]     = GenerateProportionalBar(totalOut, maxOutliers);
                    row["% Outliers"]  = pctOut;
                }

                outputTable.Rows.Add(row);
            }

            if (outputTable.Rows.Count > 0)
                outputTable.Output();
            else
                Info("No results to display.");
        }
    }
}
