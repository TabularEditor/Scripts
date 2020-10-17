/*
 * Title: Hide columns on the many side of a relationship  
 *
 * Author: Matt Allington, https://exceleratorbi.com.au  
 *
 * it is dangerous to use columns on the many side of a relationship as it can 
 * produce unexpected results, so it is a best practice to hide these columns
 * to discourage their use in reports.
 *
 * This script does not play well with Auto Calendar Tables (because your Calendar Date 
 * column is technically on the many side of the hidden Auto Calendar Tables.  
 * Make sure you turn off Auto Calendar Tables first before using this script.
 */

// Hide all columns on many side of a join
foreach (var r in Model.Relationships)
{ // hide all columns on the many side of a join
    var c = r.FromColumn.Name;
    var t = r.FromTable.Name;
    Model.Tables[t].Columns[c].IsHidden = true;
}
