// Title: Auto-create explicit measures from all columns in all tables that have qualifying aggregation functions assigned 
//  
// Author: Tom Martens, twitter.com/tommartens68
// 
// This script, when executed, will loop through all the tables and creates explicit measure for all the columns with qualifying
// aggregation functions.
// The qualifiyng aggregation functions are SUM, COUNT, MIN, MAX, AVERAGE.
// This script can create a lot of measures, as by default the aggregation function for columns with a numeric data type is SUM.
// So, it is a good idea to check all columns for the proper aggregation type, e.g. the aggregation type of id columns 
// should be set to None, as it does not make any sense to aggregate id columns.
// An annotation:CreatedThrough is created with a value:CreateExplicitMeasures this will help to identify the measures createed
// using this script.
// What is missing, the list below shows what might be coming in subsequent iterations of the script:
// - the base column property hidden is not set to true
// - no black list is used to prevent the creation of unwanted measures

// ***************************************************************************************************************
//the following variables are allowing to control the script
var overwriteExistingMeasures = 0; // 1 overwrites existing measures, 0 preserves existing measures

var measureNameTemplate = "{0} ({1}) - {2}"; // String.Format is used to create the measure name. 
//{0} will be replaced with the columnname (c.Name), {1} will be replaced with the aggregation function, and last but not least
//{2} will be replaced with the tablename (t.Name). Using t.Name is necessary to create a distinction between measure names if
//columns with the same name exist in different tables.
//Assuming the column name inside the table "Fact Sale" is "Sales revenue" and the aggregation function is SUM 
//the measure name will be: "Sales revenue (Sum) - Fact Sale"

//store aggregation function that qualify for measure creation to the hashset aggFunctions
var aggFunctions = new HashSet<AggregateFunction>{
    AggregateFunction.Sum,
    AggregateFunction.Count,
    AggregateFunction.Min,
    AggregateFunction.Max,
    AggregateFunction.Average
};

// ***************************************************************************************************************
//all the stuff below this line should not be altered 
//of course this is not valid if you have to fix my errors, make the code more efficient, 
//or you have a thorough understanding of what you are doing

//store all the existing measures to the list ListOfMeasures
var listOfMeasures = new List<string>();
foreach( var m in Model.AllMeasures ) {
    listOfMeasures.Add( m.Name );
}

//loop across all tables
foreach( var t in Model.Tables ) {
    
    //loop across all columns of the current table t
    foreach( var c in t.Columns ) {
        
        var currAggFunction = c.SummarizeBy; //cache the aggregation function of the current column c
        
        if( aggFunctions.Contains(currAggFunction) ) //check if the current aggregation function qualifies for measure aggregation
        {
            var newMeasureName = String.Format( measureNameTemplate , c.Name , currAggFunction , t.Name ); // Name of the new Measure
            var posInListOfMeasures = listOfMeasures.IndexOf( newMeasureName ); //check if the new measure already exists <> -1
            
            //create the measure if the current aggregation function qualifies for measure creation
            if( ( posInListOfMeasures == -1 || overwriteExistingMeasures == 1 )) 
            {    
                if( overwriteExistingMeasures == 1 ) 
                {
                    foreach( var m in Model.AllMeasures.Where( m => m.Name == newMeasureName ).ToList() ) 
                    {
                        m.Delete();
                    }
                }
                
                var newMeasure = t.AddMeasure
                (
                    newMeasureName                                                                      // Name of the new Measure
                    , "" + currAggFunction.ToString().ToUpper() + "(" + c.DaxObjectFullName + ")"       // DAX expression
                );
                
                newMeasure.SetAnnotation( "CreatedThrough" , "CreateExplicitMeasures" ); // flag the measures created throught this script
                
            }
        }    
    }        
}