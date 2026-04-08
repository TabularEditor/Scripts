// Tabular Editor C# script to summarize data for selected tables, columns and measures with
// Unicode bar chart visualization.
// Vibe-coded by Ruben Van de Voorde with Claude Code. Adapted from the original
// PreviewColumnsAndMeasures.csx by Kurt Buhler (https://gist.github.com/data-goblin/6ff37760cb35b793801c19a6a0ad73b0).
//
// Instructions
// ------------
// 1. Save this script as a macro with a context of 'Table', 'Column' and 'Measure'
// 2. Configure a keyboard shortcut for the macro if using Tabular Editor 3
// 3. Select any combination of tables, columns and measures and run the script
//
// Output columns (varies by selection)
// -------------------------------------
// Tables only:       Table name, # Rows, Bars (proportional row count comparison)
// Columns only:      Distinct value combinations per column, # Rows per combination, Bars
// Measures only:     Measure name, evaluated result, Bars
// Columns + Measures: Distinct value combinations per column, evaluated result per measure, Bars

// ----------------------------------------------------------------------------------------------------------//
// Timing infrastructure

var _stopwatch = System.Diagnostics.Stopwatch.StartNew();

string FormatTiming()
{
    return $"({_stopwatch.ElapsedMilliseconds / 1000.0:F2}s⏱️)";
}

// ----------------------------------------------------------------------------------------------------------//
// Get table names
var _TablesList = new List<string>();
foreach ( var _SelectedTable in Selected.Tables )
{
    _TablesList.Add(
        "\nADDCOLUMNS (\n{" + 
        @"""" + _SelectedTable.Name + @"""" + 
        "},\n" + 
        @"""" + "Row Count" + @"""" + 
        ",\nCOUNTROWS ( " + 
        _SelectedTable.DaxObjectFullName + 
        " ))");
}

// Get column names
var _ColumnsList = new List<string>();
foreach ( var _SelectedColumn in Selected.Columns )
{
    _ColumnsList.Add(_SelectedColumn.DaxObjectFullName);
}
string _Columns = String.Join(",", _ColumnsList );

// Get measure names
var _MeasuresList = new List<string>();
var _MeasuresOnlyList = new List<string>();
foreach ( var _SelectedMeasure in Selected.Measures )
{
    // Create a syntax for evaluating objects when measures + columns are selected
    _MeasuresList.Add( @"""@" + _SelectedMeasure.Name + @"""" );
    _MeasuresList.Add(_SelectedMeasure.DaxObjectFullName);

    // Create a syntax for evaluating objects when only measures are selected
    _MeasuresOnlyList.Add( 
        "\nADDCOLUMNS (\n{" + 
        @"""" + _SelectedMeasure.Name + @"""" + 
        "},\n" + 
        @"""" + "Result" + @"""" + 
        ",\n" + 
        _SelectedMeasure.DaxObjectFullName + ")");
}
string _Measures = String.Join(",", _MeasuresList );

// Count selected objects
int _NrTables = Selected.Tables.Count();
int _NrMeasures = Selected.Measures.Count();
int _NrColumns = Selected.Columns.Count();

// Info message for unsupported combinations
string _InfoMessage = "Unsupported selection for the VisualizeSummaryData macro. Supported combinations:\n" +
                      "• Tables only\n" +
                      "• Measures only\n" +
                      "• Columns from the same table\n" +
                      "• Columns & measures";

// ----------------------------------------------------------------------------------------------------------//
// Helper to strip brackets from DAX column names (keeps model column names like 'Table'[Column])
Func<string, string> StripBrackets = (name) =>
    name.StartsWith("[") && name.EndsWith("]") ? name.Substring(1, name.Length - 2) : name;

// Helper function to process DataTable and strip brackets from column names
System.Data.DataTable StripBracketsFromTable(System.Data.DataTable result)
{
    if (result == null) return null;

    var newTable = new System.Data.DataTable();
    var columnMapping = new List<(string origName, string newName)>();

    for (int i = 0; i < result.Columns.Count; i++)
    {
        var col = result.Columns[i];
        string cleanName = StripBrackets(col.ColumnName);
        columnMapping.Add((col.ColumnName, cleanName));
        newTable.Columns.Add(cleanName, col.DataType);
    }

    foreach (System.Data.DataRow row in result.Rows)
    {
        var newRow = newTable.NewRow();
        foreach (var (origName, newName) in columnMapping)
        {
            newRow[newName] = row[origName];
        }
        newTable.Rows.Add(newRow);
    }

    return newTable;
}

// ----------------------------------------------------------------------------------------------------------//
// Helper function to generate bar chart for a given count
string GenerateBar(long count, long maxCount)
{
    if (maxCount == 0 || count == 0)
        return "";

    // Scale to 12 characters max (12 has divisors 1,2,3,4,6,12 for accurate percentages)
    int barLength = Math.Max(1, (int)Math.Round((double)count / maxCount * 12));
    return new string('█', barLength);
}

// Helper function to generate bar chart for measure values (uses absolute value for scaling)
string GenerateMeasureBar(double value, double maxAbsValue)
{
    if (maxAbsValue == 0)
        return "";

    // Scale to 12 characters max based on absolute value
    int barLength = (int)Math.Round(Math.Abs(value) / maxAbsValue * 12);
    barLength = Math.Max(0, Math.Min(12, barLength));

    if (barLength == 0)
        return "";

    return new string('█', barLength);
}

// Helper function to add chart column and output results with visualization
// Generalized to preserve all original columns from the input DataTable
void OutputWithChart(System.Data.DataTable result)
{
    // Check if result is null
    if (result == null)
    {
        Info("ERROR: Result is null");
        return;
    }

    // Find the Row Count column (with brackets)
    if (!result.Columns.Contains("[Row Count]"))
    {
        result.Output();
        return;
    }

    // Get all row count values and find maximum
    var rowCounts = new List<long>();
    foreach (System.Data.DataRow row in result.Rows)
    {
        if (row["[Row Count]"] != DBNull.Value)
        {
            rowCounts.Add(Convert.ToInt64(row["[Row Count]"]));
        }
    }

    if (rowCounts.Count == 0)
    {
        result.Output();
        return;
    }

    long maxCount = rowCounts.Max();

    // Create a new DataTable preserving all original columns with clean names
    var newTable = new System.Data.DataTable();
    var columnMapping = new List<(string origName, string newName)>();
    for (int i = 0; i < result.Columns.Count; i++)
    {
        var col = result.Columns[i];
        string cleanName = StripBrackets(col.ColumnName);
        columnMapping.Add((col.ColumnName, cleanName));
        newTable.Columns.Add(cleanName, col.DataType);
    }

    // Note: Bar column padding can vary across screen sizes, DPI and scaling
    // settings. If bars appear truncated or have excess whitespace, adjust the
    // padding values below.

    // Add the Bars column at the end with timing
    double elapsedSeconds = _stopwatch.ElapsedMilliseconds / 1000.0;
    int barsPadding = elapsedSeconds < 10 ? 13 : 11;
    string chartColumnName = $"Bars {FormatTiming()}" + new string('\u00A0', barsPadding);
    newTable.Columns.Add(chartColumnName, typeof(string));

    // Copy rows and add bar charts
    foreach (System.Data.DataRow row in result.Rows)
    {
        var newRow = newTable.NewRow();

        // Copy all original columns using mapping
        foreach (var (origName, newName) in columnMapping)
        {
            newRow[newName] = row[origName];
        }

        // Add bar chart
        long rowCount = Convert.ToInt64(row["[Row Count]"]);
        newRow[chartColumnName] = GenerateBar(rowCount, maxCount);

        newTable.Rows.Add(newRow);
    }

    // Output the new table
    newTable.Output();
}

// ----------------------------------------------------------------------------------------------------------//
// Result if only one table is selected
if ( _NrTables == 1 && _NrColumns == 0 && _NrMeasures == 0 )
{
    var _SelectedTable = Selected.Tables.First();
    string _dax =
        "SELECTCOLUMNS(\n" +
        "ADDCOLUMNS (\n{" +
        @"""" + _SelectedTable.Name + @"""" +
        "},\n" +
        @"""" + "Row Count" + @"""" +
        ",\nCOUNTROWS ( " +
        _SelectedTable.DaxObjectFullName +
        " ))," +
        @"""" + "Table Name" + @"""" +
        ", [Value]," +
        @"""" + "Row Count" + @"""" +
        ", [Row Count])";

    System.Data.DataTable result;
    try { result = EvaluateDax(_dax) as System.Data.DataTable; }
    catch { Info("DAX query failed. Check that the selected table is accessible."); return; }
    OutputWithChart(result);
}

// ----------------------------------------------------------------------------------------------------------//
// Result if multiple tables are selected
else if ( _NrTables > 1 && _NrColumns == 0 && _NrMeasures == 0 )
{
    string _dax =
        "SELECTCOLUMNS( UNION ( " +
        String.Join(",", _TablesList ) + ")," +
        @"""" + "Table Name" + @"""" +
        ", [Value]," +
        @"""" + "Row Count" + @"""" +
        ", [Row Count])";

    System.Data.DataTable result;
    try { result = EvaluateDax(_dax) as System.Data.DataTable; }
    catch { Info("DAX query failed. Check that the selected tables are accessible."); return; }
    OutputWithChart(result);
}

// ----------------------------------------------------------------------------------------------------------//
// Result if no tables/columns selected and more than one measure selected
else if ( _NrTables == 0 && _NrColumns == 0 && _NrMeasures > 1 )
{
    // Evaluate each measure as a separate row
    string _dax =
        "SELECTCOLUMNS( UNION ( " +
        String.Join(",", _MeasuresOnlyList ) + ")," +
        @"""" + "Measure Name" + @"""" +
        ", [Value]," +
        @"""" + "Measure Result" + @"""" +
        ", [Result])" ;

    System.Data.DataTable result;
    try { result = EvaluateDax(_dax) as System.Data.DataTable; }
    catch { Info("DAX query failed. Check that the selected measures are valid."); return; }

    // Find max absolute value for scaling bars
    var measureValues = new List<double>();
    foreach (System.Data.DataRow row in result.Rows)
    {
        if (row["[Measure Result]"] != DBNull.Value)
        {
            measureValues.Add(Convert.ToDouble(row["[Measure Result]"]));
        }
    }
    double maxAbsValue = measureValues.Count > 0 ? measureValues.Max(v => Math.Abs(v)) : 0;

    // Build new table with bars
    var newTable = new System.Data.DataTable();
    newTable.Columns.Add("Measure Name", typeof(string));
    newTable.Columns.Add("Measure Result", typeof(double));

    // Add bars column with timing and padding
    double elapsedSeconds = _stopwatch.ElapsedMilliseconds / 1000.0;
    int barsPadding = elapsedSeconds < 10 ? 13 : 11;
    string barsColumnName = $"Bars {FormatTiming()}" + new string('\u00A0', barsPadding);
    newTable.Columns.Add(barsColumnName, typeof(string));

    foreach (System.Data.DataRow row in result.Rows)
    {
        var newRow = newTable.NewRow();
        newRow["Measure Name"] = row["[Measure Name]"];
        newRow["Measure Result"] = row["[Measure Result]"];

        if (row["[Measure Result]"] != DBNull.Value)
        {
            double value = Convert.ToDouble(row["[Measure Result]"]);
            newRow[barsColumnName] = GenerateMeasureBar(value, maxAbsValue);
        }
        else
        {
            newRow[barsColumnName] = "";
        }

        newTable.Rows.Add(newRow);
    }

    newTable.Output();
}

// ----------------------------------------------------------------------------------------------------------//
// Result if no tables/columns selected and exactly one measure selected
else if ( _NrTables == 0 && _NrColumns == 0 && _NrMeasures == 1 )
{
    // Evaluate measure as a single row
    string _dax =
        "SELECTCOLUMNS( " +
        String.Join(",", _MeasuresOnlyList ) + "," +
        @"""" + "Measure Name" + @"""" +
        ", [Value]," +
        @"""" + "Measure Result" + @"""" +
        ", [Result])" ;

    System.Data.DataTable result;
    try { result = EvaluateDax(_dax) as System.Data.DataTable; }
    catch { Info("DAX query failed. Check that the selected measure is valid."); return; }

    // Build new table with bars (single measure = full bar)
    var newTable = new System.Data.DataTable();
    newTable.Columns.Add("Measure Name", typeof(string));
    newTable.Columns.Add("Measure Result", typeof(double));

    // Add bars column with timing and padding
    double elapsedSeconds = _stopwatch.ElapsedMilliseconds / 1000.0;
    int barsPadding = elapsedSeconds < 10 ? 13 : 11;
    string barsColumnName = $"Bars {FormatTiming()}" + new string('\u00A0', barsPadding);
    newTable.Columns.Add(barsColumnName, typeof(string));

    if (result.Rows.Count > 0)
    {
        var row = result.Rows[0];
        var newRow = newTable.NewRow();
        newRow["Measure Name"] = row["[Measure Name]"];
        newRow["Measure Result"] = row["[Measure Result]"];

        // Single measure always gets full bar (if non-zero)
        if (row["[Measure Result]"] != DBNull.Value)
        {
            double value = Convert.ToDouble(row["[Measure Result]"]);
            newRow[barsColumnName] = value != 0 ? new string('█', 12) : "";
        }
        else
        {
            newRow[barsColumnName] = "";
        }

        newTable.Rows.Add(newRow);
    }

    newTable.Output();
}

// ----------------------------------------------------------------------------------------------------------//
// Result if a combination of measures and columns are selected (no tables)
else if ( _NrTables == 0 && _NrMeasures > 0 && _NrColumns > 0 )
{
    // Summarize selected columns + measures with DAX
    string _dax =
        "SUMMARIZECOLUMNS ( " + _Columns + ", " + _Measures + ")";

    System.Data.DataTable result;
    try { result = EvaluateDax(_dax) as System.Data.DataTable; }
    catch { Info("DAX query failed. Check that the selected columns and measures are valid."); return; }

    // Get measure names (columns will be named [@MeasureName] in result)
    var measureNames = Selected.Measures.Select(m => m.Name).ToList();

    // Calculate max absolute value for each measure
    var maxAbsValues = new Dictionary<string, double>();
    foreach (var measureName in measureNames)
    {
        string colName = "[@" + measureName + "]";
        if (result.Columns.Contains(colName))
        {
            double maxAbs = 0;
            foreach (System.Data.DataRow row in result.Rows)
            {
                if (row[colName] != DBNull.Value)
                {
                    maxAbs = Math.Max(maxAbs, Math.Abs(Convert.ToDouble(row[colName])));
                }
            }
            maxAbsValues[measureName] = maxAbs;
        }
    }

    // Build new table: column columns first, then measure + bar pairs
    var newTable = new System.Data.DataTable();

    // Add column columns (non-measure columns)
    var columnColNames = new List<string>();
    foreach (System.Data.DataColumn col in result.Columns)
    {
        if (!col.ColumnName.StartsWith("[@"))
        {
            string cleanName = StripBrackets(col.ColumnName);
            newTable.Columns.Add(cleanName, col.DataType);
            columnColNames.Add(col.ColumnName);
        }
    }

    // Add measure + bar column pairs
    double elapsedSeconds = _stopwatch.ElapsedMilliseconds / 1000.0;
    int barsPadding = 0;
    bool firstBar = true;

    foreach (var measureName in measureNames)
    {
        string origColName = "[@" + measureName + "]";
        if (result.Columns.Contains(origColName))
        {
            // Add measure column
            newTable.Columns.Add(measureName, result.Columns[origColName].DataType);

            // Add bar column (timing on first bar column only)
            string barColName = firstBar
                ? $"{measureName} Bars {FormatTiming()}" + new string('\u00A0', barsPadding)
                : $"{measureName} Bars" + new string('\u00A0', barsPadding + 12);
            newTable.Columns.Add(barColName, typeof(string));
            firstBar = false;
        }
    }

    // Populate rows
    foreach (System.Data.DataRow row in result.Rows)
    {
        var newRow = newTable.NewRow();

        // Copy column columns
        foreach (var origColName in columnColNames)
        {
            string cleanName = StripBrackets(origColName);
            newRow[cleanName] = row[origColName];
        }

        // Copy measure values and generate bars
        foreach (var measureName in measureNames)
        {
            string origColName = "[@" + measureName + "]";
            if (result.Columns.Contains(origColName))
            {
                newRow[measureName] = row[origColName];

                // Find the bar column for this measure
                string barColPrefix = measureName + " Bars";
                var barCol = newTable.Columns.Cast<System.Data.DataColumn>()
                    .FirstOrDefault(c => c.ColumnName.StartsWith(barColPrefix));

                if (barCol != null && row[origColName] != DBNull.Value)
                {
                    double value = Convert.ToDouble(row[origColName]);
                    double maxAbs = maxAbsValues.ContainsKey(measureName) ? maxAbsValues[measureName] : 0;
                    newRow[barCol.ColumnName] = GenerateMeasureBar(value, maxAbs);
                }
                else if (barCol != null)
                {
                    newRow[barCol.ColumnName] = "";
                }
            }
        }

        newTable.Rows.Add(newRow);
    }

    newTable.Output();
}

// ----------------------------------------------------------------------------------------------------------//
// Result if no tables/measures and only columns are selected
else if ( _NrTables == 0 && _NrMeasures == 0 && _NrColumns > 0 )
{
    var _FirstTable = Selected.Columns.First().Table;
    bool _SameTable = Selected.Columns.All(c => c.Table == _FirstTable);
    
    if (_SameTable)
    {
        string _dax =
            "GROUPBY (\n" +
            _FirstTable.DaxObjectFullName + ",\n" +
            _Columns + ",\n" +
            @"""Row Count"", COUNTX ( CURRENTGROUP (), 1 ))";

        System.Data.DataTable result;
        try { result = EvaluateDax(_dax) as System.Data.DataTable; }
        catch { Info("DAX query failed. Check that the selected columns are accessible."); return; }
        OutputWithChart(result);
    }
    else
    {   // Columns from different tables; unsupported
        Info(_InfoMessage);
    }
}

// ----------------------------------------------------------------------------------------------------------//
// Fallback for any unsupported combination
else
{
    Info(_InfoMessage);
}