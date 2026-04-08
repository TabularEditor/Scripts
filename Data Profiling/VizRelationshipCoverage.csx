// Tabular Editor C# script to check FK coverage for all model relationships — in both directions.
// For each relationship, checks:
//   • FK side  — what % of FK values (many side) exist in the PK column (one side)
//   • Dim side — what % of PK values (one side) are referenced by at least one FK value
// Unmatched FK values → show as BLANK in reports, breaking relationship integrity.
// Unmatched PK values → dimension rows that never appear in the fact table.
// Vibe-coded by Ruben Van de Voorde with Claude Code.
//
// Instructions
// ------------
// 1. Save this script as a macro with no specific context (runs on the model)
// 2. No selection required — analyzes all relationships in the model
// 3. Results sorted by worst coverage of the two directions: most problematic relationships surface first
//
// Output columns
// --------------
// Relationship   — "FactTable[FKCol] → DimTable[PKCol]"
// Active         — whether the relationship is active
// # FK Values      — distinct non-blank values in the FK (many) column
// # FK Unmatched   — FK values with no match in the PK column
// % FK Coverage    — (FK Values - FK Unmatched) / FK Values
// % FK (Bars)      — bar chart for FK coverage
// # PK Values      — distinct non-blank values in the PK (one) column
// # PK Unused      — PK values that appear in no FK row
// % PK Coverage    — (PK Values - PK Unused) / PK Values
// % PK (Bars)      — bar chart for PK coverage
//
// Note: BLANK FK values are not counted as unmatched — they map to the blank row in the dimension,
// which is expected behaviour for rows without a related dimension entry.
// Note: Large fact tables make this expensive. Each relationship fires one DAX query with two EXCEPT calls.

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

var allRelationships = Model.Relationships.ToList();

if (allRelationships.Count == 0)
{
    Info("No relationships found in this model.");
}
else
{
    // Per-relationship results
    var results = new List<(string label, bool isActive, long distinctFK, long orphans, long distinctPK, long unusedDim)>();

    foreach (var rel in allRelationships)
    {
        var fromCol = rel.FromColumn;  // Many side (FK)
        var toCol   = rel.ToColumn;    // One side (PK / unique key)

        string fromDax = fromCol.DaxObjectFullName;
        string toDax   = toCol.DaxObjectFullName;
        string label   = $"{fromCol.Table.Name}[{fromCol.Name}] \u2192 {toCol.Table.Name}[{toCol.Name}]";
        bool   active  = rel.IsActive;

        string dax = $@"
VAR _distinctFK  = DISTINCTCOUNT({fromDax})
VAR _orphans     = COUNTROWS(EXCEPT(VALUES({fromDax}), VALUES({toDax})))
VAR _distinctPK  = DISTINCTCOUNT({toDax})
VAR _unusedDim   = COUNTROWS(EXCEPT(VALUES({toDax}), VALUES({fromDax})))
RETURN ROW(
    ""DistinctFK"",  _distinctFK,
    ""Orphans"",     _orphans,
    ""DistinctPK"",  _distinctPK,
    ""UnusedDim"",   _unusedDim
)";

        try
        {
            var result = EvaluateDax(dax) as System.Data.DataTable;
            if (result != null && result.Rows.Count > 0)
            {
                var r       = result.Rows[0];
                long fk     = r["[DistinctFK]"] == DBNull.Value ? 0 : Convert.ToInt64(r["[DistinctFK]"]);
                long orphans = r["[Orphans]"]   == DBNull.Value ? 0 : Convert.ToInt64(r["[Orphans]"]);
                long pk     = r["[DistinctPK]"] == DBNull.Value ? 0 : Convert.ToInt64(r["[DistinctPK]"]);
                long unused = r["[UnusedDim]"]  == DBNull.Value ? 0 : Convert.ToInt64(r["[UnusedDim]"]);
                results.Add((label, active, fk, orphans, pk, unused));
            }
        }
        catch
        {
            // Relationship query failed — add as zero so it still appears
            results.Add((label, active, 0, 0, 0, 0));
        }
    }

    // Sort by worst coverage of the two directions ascending — most problematic relationships surface first
    results.Sort((a, b) =>
    {
        double fkCovA  = a.distinctFK > 0 ? (double)(a.distinctFK - a.orphans)   / a.distinctFK : 1.0;
        double dimCovA = a.distinctPK > 0 ? (double)(a.distinctPK - a.unusedDim) / a.distinctPK : 1.0;
        double covA    = Math.Min(fkCovA, dimCovA);

        double fkCovB  = b.distinctFK > 0 ? (double)(b.distinctFK - b.orphans)   / b.distinctFK : 1.0;
        double dimCovB = b.distinctPK > 0 ? (double)(b.distinctPK - b.unusedDim) / b.distinctPK : 1.0;
        double covB    = Math.Min(fkCovB, dimCovB);

        return covA.CompareTo(covB);
    });

    // Note: Bar column padding can vary across screen sizes, DPI and scaling
    // settings. If bars appear truncated or have excess whitespace, adjust the
    // padding values below.

    // Build output DataTable
    var outputTable = new System.Data.DataTable();

    string relHeader = $"Relationship {FormatTiming()}";
    outputTable.Columns.Add(relHeader,       typeof(string));
    outputTable.Columns.Add("Active",        typeof(string));
    outputTable.Columns.Add("# FK Values",     typeof(long));
    outputTable.Columns.Add("# FK Unmatched", typeof(long));
    outputTable.Columns.Add("% FK Coverage",  typeof(double));

    string fkBarHeader = "% FK (Bars)" + new string('\u00A0', 20);
    outputTable.Columns.Add(fkBarHeader,      typeof(string));

    outputTable.Columns.Add("# PK Values",     typeof(long));
    outputTable.Columns.Add("# PK Unused",  typeof(long));
    outputTable.Columns.Add("% PK Coverage",   typeof(double));

    string pkBarHeader = "% PK (Bars)" + new string('\u00A0', 19);
    outputTable.Columns.Add(pkBarHeader,       typeof(string));

    foreach (var (label, isActive, fk, orphans, pk, unusedDim) in results)
    {
        double fkCov = fk > 0 ? Math.Round((double)(fk - orphans)   / fk * 100, 1) : 0.0;
        double pkCov = pk > 0 ? Math.Round((double)(pk - unusedDim) / pk * 100, 1) : 0.0;

        var row = outputTable.NewRow();
        row[relHeader]        = label;
        row["Active"]         = isActive ? "\u2612" : "\u2610";
        row["# FK Values"]    = fk;
        row["# FK Unmatched"] = orphans;
        row["% FK Coverage"]  = fkCov;
        row[fkBarHeader]      = GeneratePercentageBar(GetBarLength(fkCov));
        row["# PK Values"]    = pk;
        row["# PK Unused"] = unusedDim;
        row["% PK Coverage"]  = pkCov;
        row[pkBarHeader]      = GeneratePercentageBar(GetBarLength(pkCov));
        outputTable.Rows.Add(row);
    }

    outputTable.Output();
}
