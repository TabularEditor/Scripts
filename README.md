# Scripts
Community repository for sharing and discussing scripts for use with Tabular Editor.

## How to contribute
Fork the repo, add your scripts and submit a pull request - it's that simple!

Scripts should use the `.csx` file extension. If you plan to submit a collection of multiple scripts, feel free to put them into a subfolder and provide a `README.md` file with additional documentation.

Please ensure that your script is thoroughly documented with a comment section at the top of the file. Feel free to use the following snippet as a template:

```csharp
/*
 * Title: Auto-generate SUM measures from columns
 * 
 * Author: Daniel Otykier, twitter.com/DOtykier
 * 
 * This script, when executed, will loop through the currently selected columns,
 * creating one SUM measure for each column and also hiding the column itself.
 */
 
// Loop through all currently selected columns:
foreach(var c in Selected.Columns)
{
    var newMeasure = c.Table.AddMeasure(
        "Sum of " + c.Name,                    // Name
        "SUM(" + c.DaxObjectFullName + ")",    // DAX expression
        c.DisplayFolder                        // Display Folder
    );
    
    // Set the format string on the new measure:
    newMeasure.FormatString = "0.00";

    // Provide some documentation:
    newMeasure.Description = "This measure is the sum of column " + c.DaxObjectFullName;

    // Hide the base column:
    c.IsHidden = true;
}
```
