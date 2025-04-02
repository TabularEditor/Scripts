// ===========================================
// Check reports for invalid object references
// ===========================================
// Author: Daniel Otykier, github.com/otykier
//
// Requirements:
//   Tabular Editor 2 v. 2.26.0 or newer
//   Tabular Eidtor 3 v. 3.21.0 or newer
//
// Script usage:
//   [x] Command line
//   [x] Interactive (model scope)
//
// Description:
//   This script can be used to check Power BI report files using the Enhanced PBIR format for
//   invalid measure references against the currently loaded semantic model. You can run the
//   script interactively (i.e. from TE2/TE3 UI), or from a command line.
//
//   When the current model in Tabular Editor is loaded from a Power BI Project (PBIP) folder
//   structure, the script will automatically locate any reports in the workspace folder, that
//   point to the current model (by examining the definition.pbir file in each - note, this
//   file must specify a datasetReference "by path", for this to work).
//
//   Alternatively, the script (in interactive mode) will prompt for the root of the workspace
//   folder of reports to scan. In command line mode, you can also specify this root folder
//   by setting an environment variable with the name: TE_WSFolder
//
//   In command line mode, the script will output Warning messages (loglevel=warning) for each
//   invalid reference found. In interactive mode, the script will show a summary of issues
//   after scanning all report files.
//
// See also:
//   https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-report#pbir-format
//   https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-report#definitionpbir

using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

string pbipModelName = null;
string rootFolder = null;

// Hack to determine if the script is running in command line mode or not:
var commandLineMode = Application.OpenForms.Count == 0;
var result = string.Empty;
var issues = 0;

rootFolder = Environment.GetEnvironmentVariable("TE_WSFolder");

if (rootFolder == null && Model.MetadataSource.Pbip == null)
{
    if(commandLineMode)
    {
        Error("Could not determine workspace folder. Set environment variable TE_WSFolder before running this script.", 0, true);
        return;
    }
    Info("Model not loaded from PBIP folder structure. Script will assume all scanned reports use this model.");
}
else
{
    // Find all report folders that point to the current model:
    pbipModelName = Model.MetadataSource.Pbip.Name;
    rootFolder = Model.MetadataSource.Pbip.RootFolder;
}

// Cache measures by name just to speed up checks:
var measureCache = Model.AllMeasures.ToDictionary(m => m.Name, m => m, StringComparer.OrdinalIgnoreCase);

// Prompt user for workspace folder if we couldn't determine it from the model location:
if (rootFolder == null)
{
    var fbd = new FolderBrowserDialog() { 
        Description = "Select workspace folder to scan for Power BI report items",
#if TE3
        UseDescriptionForTitle = true
#endif
    };
    if(fbd.ShowDialog() == DialogResult.Cancel) return;
    rootFolder = fbd.SelectedPath;
}

// ------------- Define a few helper functions ----------------

Func<string, bool> ValidReportDefinition = (string reportDefinitionFilePath) =>
{
    var definitionJson = File.ReadAllText(reportDefinitionFilePath);
    return (pbipModelName == null || definitionJson.Contains(pbipModelName)) && definitionJson.Contains("4.0");
};

Action<string, string, JToken> OutputError = (string errorMessage, string jsonFileName, JToken token) =>
{
    var fileDetails = "in " + jsonFileName + " (line " + (token as IJsonLineInfo).LineNumber + ")";
    if(commandLineMode)
    {
        Error(errorMessage + " " + fileDetails + " " + token.Path, 0, true);
    }
    else
    {
        result += "  " + errorMessage + Environment.NewLine;
        result += "    " + fileDetails + Environment.NewLine;
        result += "    " + token.Path + Environment.NewLine;
    }
    issues++;
};

Action<string, JValue> CheckMeasureRef = (string file, JValue measureRef) =>
{
    // Check if the provided JValue 
    var parentObj = measureRef.Parent.Parent as JObject;
    var sourceRefObj = parentObj.SelectToken("Expression.SourceRef") as JObject;
    // This means that the measure is a report-level measure, so we don't need to check if it exists in the model:
    if(sourceRefObj.ContainsKey("Schema") && sourceRefObj["Schema"].ToString() == "extension") return;

    var tableRef = sourceRefObj["Entity"];
    var tableName = tableRef.ToString();
    var measureName = measureRef.ToString();
    Measure measure;
    if(!measureCache.TryGetValue(measureName, out measure))
    {
        OutputError(string.Format("Measure not found: [{0}]", measureName), file, measureRef);
        return;
    }
    else if(!measure.Table.Name.EqualsI(tableName))
    {
        OutputError(string.Format("Measure [{0}] not found on table '{1}', actual table: '{2}'", measureName, tableName, measure.Table.Name), file, tableRef);
    }
};

Action<string> ScanReport = (string reportFolder) =>
{
    if(commandLineMode)
        Info("Scanning " + reportFolder + "...");
    else
        result += "Scanning " + reportFolder + "..." + Environment.NewLine;
    
    // Loop through all visual json files in the folder structure, to find invalid semantic model object refs:
    foreach(var jsonFile in Directory.EnumerateFiles(Path.Combine(reportFolder, "definition"), "*.json", SearchOption.AllDirectories))
    {
        var parsed = JObject.Parse(File.ReadAllText(jsonFile));
        var measureRefs = parsed.SelectTokens("$..Measure.Property").OfType<JValue>();
        foreach(var measureRef in measureRefs) CheckMeasureRef(jsonFile.Substring(rootFolder.Length), measureRef);
    }
};
// ----------------------------------------------

var reportFolders = Directory.EnumerateFiles(rootFolder, "definition.pbir", SearchOption.AllDirectories)
    .Where(ValidReportDefinition)
    .Select(f => new FileInfo(f).Directory.FullName).ToList();

// Loop through all report folders and check each:
foreach(var reportFolder in reportFolders) ScanReport(reportFolder);

if(!commandLineMode)
{
    result += Environment.NewLine + "Issues found: " + issues;
    result.Output();
}
else
{
    if(issues == 0) Info("No issues found!");
    else Warning("Issues found: " + issues, 0, true);
}