/*
 * Title: Replace Text Strings that appear in many measures
 * 
 * Author: Matt Allington http://xbi.com.au
 * 
 * This script, when executed, will loop through the currently selected measures
 * and replace the FromString with the ToString.
 */
 
/ Replace Text Strings that appear in many measures
	var FromString = "CALCULATE(SUM(Sales[ExtendedAmount])";
	var ToString = "CALCULATE([Total Sales]";
	foreach (var m in Model.AllMeasures)
    	{
           m.Expression = m.Expression.Replace(FromString,ToString);
    	}

