// Tabular Editor C# script to detect unusually large gaps between consecutive dates.
// For each selected date column, computes gaps between consecutive distinct dates and
// flags gaps that exceed Q3 + 1.5×IQR — the same IQR method used by VizOutliers.
// Vibe-coded by Ruben Van de Voorde with Claude Code.
//
// Instructions
// ------------
// 1. Save this script as a macro with a context of 'Column'
// 2. Configure a keyboard shortcut for the macro if using Tabular Editor 3
// 3. Select one or more date/datetime columns from the SAME table
// 4. Non-date columns are silently skipped
//
// Output columns
// --------------
// # Distinct Dates       — distinct non-blank date values in the column
// Date Range (Days)      — span in days from min to max date
// Typical Spacing (Days) — median spacing between consecutive dates (describes the baseline cadence)
// Max Spacing (Days)     — largest single spacing observed
// Upper Fence            — Q3 + 1.5 × IQR; threshold above which a spacing is flagged as a large gap
// # Large Gaps           — spacings exceeding the upper fence
// % Large Gaps           — share of all spacings that are large, shown as bar chart
//                     (conditional: only if any large gaps found)
//
// Note: gaps are computed on DISTINCT dates, so duplicate date rows do not inflate
// the gap count. A daily fact table with weekends missing will show gaps of 2–3 days
// as the baseline; the IQR fence adapts accordingly.
// Note: high-cardinality date columns make this expensive — each column fires one
// DAX query that iterates over all distinct dates to find consecutive pairs.

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
    var firstTable = Selected.Columns.First().Table;

    if (!Selected.Columns.All(c => c.Table == firstTable))
    {
        Info("Please select columns from the same table only.");
    }
    else
    {
        var dateCols = Selected.Columns
            .Where(c => c.DataType == DataType.DateTime)
            .ToList();

        if (dateCols.Count == 0)
        {
            Info("No date/datetime columns in selection. VizDateGaps only analyzes date columns.");
        }
        else
        {
            string tableDaxName = firstTable.DaxObjectFullName;

            var results = new List<(string name, long distinctDates, long rangeDays, double medianGap, double maxGap, double upperFence, long totalGaps, long largeGaps)>();

            foreach (var col in dateCols)
            {
                string colDax = col.DaxObjectFullName;

                string dax = $@"
VAR _distinctDates =
    DISTINCT(SELECTCOLUMNS(FILTER({tableDaxName}, NOT ISBLANK({colDax})), ""d"", {colDax}))
VAR _withNext =
    ADDCOLUMNS(
        _distinctDates,
        ""NextDate"",
        VAR _cur = [d]
        RETURN MINX(FILTER(_distinctDates, [d] > _cur), [d])
    )
VAR _gaps =
    SELECTCOLUMNS(
        FILTER(_withNext, NOT ISBLANK([NextDate])),
        ""GapDays"", DATEDIFF([d], [NextDate], DAY)
    )
VAR _distinctCt = COUNTROWS(_distinctDates)
VAR _minDate    = MINX(_distinctDates, [d])
VAR _maxDate    = MAXX(_distinctDates, [d])
VAR _rangeDays  = DATEDIFF(_minDate, _maxDate, DAY)
VAR _totalGaps  = COUNTROWS(_gaps)
VAR _q1         = PERCENTILEX.INC(_gaps, [GapDays], 0.25)
VAR _q3         = PERCENTILEX.INC(_gaps, [GapDays], 0.75)
VAR _iqr        = _q3 - _q1
VAR _upper      = _q3 + 1.5 * _iqr
VAR _median     = PERCENTILEX.INC(_gaps, [GapDays], 0.5)
VAR _maxGap     = MAXX(_gaps, [GapDays])
VAR _largeGaps  = COUNTROWS(FILTER(_gaps, [GapDays] > _upper))
RETURN ROW(
    ""DistinctDates"", _distinctCt,
    ""RangeDays"",     _rangeDays,
    ""MedianGap"",     _median,
    ""MaxGap"",        _maxGap,
    ""UpperFence"",    _upper,
    ""TotalGaps"",     _totalGaps,
    ""LargeGaps"",     _largeGaps
)";

                try
                {
                    var result = EvaluateDax(dax) as System.Data.DataTable;
                    if (result != null && result.Rows.Count > 0)
                    {
                        var r            = result.Rows[0];
                        long distinct    = r["[DistinctDates]"] == DBNull.Value ? 0 : Convert.ToInt64(r["[DistinctDates]"]);
                        long range       = r["[RangeDays]"]     == DBNull.Value ? 0 : Convert.ToInt64(r["[RangeDays]"]);
                        double median    = r["[MedianGap]"]     == DBNull.Value ? 0 : Convert.ToDouble(r["[MedianGap]"]);
                        double maxGap    = r["[MaxGap]"]        == DBNull.Value ? 0 : Convert.ToDouble(r["[MaxGap]"]);
                        double upper     = r["[UpperFence]"]    == DBNull.Value ? 0 : Convert.ToDouble(r["[UpperFence]"]);
                        long totalGaps   = r["[TotalGaps]"]     == DBNull.Value ? 0 : Convert.ToInt64(r["[TotalGaps]"]);
                        long largeGaps   = r["[LargeGaps]"]     == DBNull.Value ? 0 : Convert.ToInt64(r["[LargeGaps]"]);
                        results.Add((col.Name, distinct, range, median, maxGap, upper, totalGaps, largeGaps));
                    }
                }
                catch
                {
                    results.Add((col.Name, 0, 0, 0, 0, 0, 0, 0));
                }
            }

            // Pre-scan for conditional columns
            bool anyLargeGaps = results.Any(r => r.largeGaps > 0);

            // Note: Bar column padding can vary across screen sizes, DPI and scaling
            // settings. If bars appear truncated or have excess whitespace, adjust the
            // padding values below.

            // Build output DataTable
            var outputTable = new System.Data.DataTable();

            string colHeader = $"Column {FormatTiming()}";
            outputTable.Columns.Add(colHeader,                  typeof(string));
            outputTable.Columns.Add("# Distinct Dates",        typeof(long));
            outputTable.Columns.Add("Date Range (Days)",        typeof(long));
            outputTable.Columns.Add("Typical Spacing (Days)",   typeof(double));
            outputTable.Columns.Add("Max Spacing (Days)",       typeof(double));
            outputTable.Columns.Add("Upper Fence",              typeof(double));
            outputTable.Columns.Add("# Large Gaps",             typeof(long));

            string barHeader = "% Large Gaps (Bars)" + new string('\u00A0', 3);
            if (anyLargeGaps)
            {
                outputTable.Columns.Add("% Large Gaps", typeof(double));
                outputTable.Columns.Add(barHeader,       typeof(string));
            }

            foreach (var (name, distinctDates, rangeDays, medianGap, maxGap, upperFence, totalGaps, largeGaps) in results)
            {
                double pctLarge = totalGaps > 0 ? Math.Round((double)largeGaps / totalGaps * 100, 1) : 0.0;

                var row = outputTable.NewRow();
                row[colHeader]             = name;
                row["# Distinct Dates"]       = distinctDates;
                row["Date Range (Days)"]      = rangeDays;
                row["Typical Spacing (Days)"] = Math.Round(medianGap, 1);
                row["Max Spacing (Days)"]     = Math.Round(maxGap, 1);
                row["Upper Fence"]         = Math.Round(upperFence, 1);
                row["# Large Gaps"]        = largeGaps;

                if (anyLargeGaps)
                {
                    row["% Large Gaps"] = pctLarge;
                    row[barHeader]      = GeneratePercentageBar(GetBarLength(pctLarge));
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
