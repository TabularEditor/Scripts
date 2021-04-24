/*
 * Title: Enable Power BI "Time intelligence" for DateTime column(s) in any Analysis Services Tabular model
 * 
 * Author: Mykola Anykienko, nicolas2008@i.ua
 * 
 * Enable Power BI "Time intelligence" (https://docs.microsoft.com/en-us/power-bi/transform-model/desktop-auto-date-time) for DateTime column(s) in any Analysis Services Tabular model
 * This script, when executed, will add "Time intelligence" support for selected columns by creating date table + relationship + variation, so that it will have expandable date hierarchy in Power BI report live connected to Analysis Services database
 */

foreach(var column in Selected.Columns)
{
    var table = column.Table;
    
    if (column.DataType == DataType.DateTime && !column.Variations.Any(v => v.DefaultHierarchy.Name == "Date Hierarchy"))  
    {
        // Generate date table name based on target column name
        var normalizedColumnName = string.Join("", column.Name.Split(new [] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpper(x[0]) + x.Substring(1)));
        var dateTableName = string.Format("{0}_{1}", table.Name, normalizedColumnName);
        
        // Add date table
        var dateTable = Model.AddCalculatedTable(dateTableName);
        dateTable.IsHidden = true;
        dateTable.ShowAsVariationsOnly = true;
        dateTable.SetAnnotation("__PBI_LocalDateTable", "true");
        dateTable.Expression = string.Format("Calendar(Date(Year(MIN('{0}'[{1}])), 1, 1), Date(Year(MAX('{0}'[{1}])), 12, 31))", table.Name, column.Name);
        
        var dateColumn = dateTable.AddCalculatedTableColumn("Date", "[Date]", null, DataType.DateTime);
        dateColumn.IsAvailableInMDX = false;
        dateColumn.IsNameInferred = true;
        dateColumn.IsDataTypeInferred = false;
        dateColumn.IsHidden = true;
        dateColumn.DataCategory = "PaddedDateTableDates";
        dateColumn.SummarizeBy = AggregateFunction.None;
        
        var dayColumn = dateTable.AddCalculatedColumn("Day", "DAY([Date])", null);
        dayColumn.DataType = DataType.Int64;
        dayColumn.IsHidden = true;
        dayColumn.DataCategory = "DayOfMonth";
        dayColumn.SummarizeBy = AggregateFunction.None;
        
        var monthNoColumn = dateTable.AddCalculatedColumn("MonthNo", "MONTH([Date])", null);
        monthNoColumn.DataType = DataType.Int64;
        monthNoColumn.IsHidden = true;
        monthNoColumn.DataCategory = "MonthOfYear";
        monthNoColumn.SummarizeBy = AggregateFunction.None;
        
        var monthColumn = dateTable.AddCalculatedColumn("Month", "FORMAT([Date], \"MMMM\")", null);
        monthColumn.DataType = DataType.String;
        monthColumn.SortByColumn = monthNoColumn;
        monthColumn.IsHidden = true;
        monthColumn.DataCategory = "Months";
        monthColumn.SummarizeBy = AggregateFunction.None;
        
        var quarterNoColumn = dateTable.AddCalculatedColumn("QuarterNo", "INT(([MonthNo] + 2) / 3)", null);
        quarterNoColumn.DataType = DataType.Int64;
        quarterNoColumn.IsHidden = true;
        quarterNoColumn.DataCategory = "QuarterOfYear";
        quarterNoColumn.SummarizeBy = AggregateFunction.None;
        
        var quarterColumn = dateTable.AddCalculatedColumn("Quarter", "\"Qtr \" & [QuarterNo]", null);
        quarterColumn.DataType = DataType.String;
        quarterColumn.SortByColumn = quarterNoColumn;
        quarterColumn.IsHidden = true;
        quarterColumn.DataCategory = "Quarters";
        quarterColumn.SummarizeBy = AggregateFunction.None;
        
        var yearColumn = dateTable.AddCalculatedColumn("Year", "YEAR([Date])", null);
        yearColumn.DataType = DataType.Int64;
        yearColumn.IsHidden = true;
        yearColumn.DataCategory = "Years";
        yearColumn.SummarizeBy = AggregateFunction.None;
        
        // Add relationship 'target column' <-> 'date table'.'Date'
        var relationship = Model.AddRelationship();
        relationship.FromCardinality = RelationshipEndCardinality.Many;
        relationship.FromColumn = column;
        relationship.ToCardinality = RelationshipEndCardinality.One;
        relationship.ToColumn = dateColumn;
        relationship.CrossFilteringBehavior = CrossFilteringBehavior.OneDirection;
        relationship.JoinOnDateBehavior = DateTimeRelationshipBehavior.DatePartOnly;
        
        // Add "Date Hierarchy" variation to 'target column'
        var dateHierarchy = dateTable.AddHierarchy("Date Hierarchy", null, new Column[0]);
        dateHierarchy.SetAnnotation("TemplateId", "DateHierarchy");
        dateHierarchy.AddLevel("Year", "Year", 0);
        dateHierarchy.AddLevel("Quarter", "Quarter", 1);
        dateHierarchy.AddLevel("Month", "Month", 2);
        dateHierarchy.AddLevel("Day", "Day", 3);
        
        var variation = Variation.CreateNew(column);
        variation.Name = "Variation";
        variation.IsDefault = true;
        variation.DefaultHierarchy = dateHierarchy;
        variation.Relationship = relationship;
    }
}