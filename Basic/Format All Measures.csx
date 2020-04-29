/*
 * Title: Format All Measures  
 *
 * Author: Matt Allington, https://exceleratorbi.com.au  
 *
 * This script loops through all the measures in your model and calls out to daxformatter.com
 * in order to format them.
 */

//Format All Measures
foreach (var m in Model.AllMeasures)
{
    m.Expression = FormatDax(m.Expression);
    /* Cycle over all measures in model and format 
    them all using DAX Formatter */
}
