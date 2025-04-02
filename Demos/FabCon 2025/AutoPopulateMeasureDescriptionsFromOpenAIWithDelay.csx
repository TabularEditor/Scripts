#r "System.Net.Http"

// ---------------------------------------------------
// GENERATE MEASURE DESCRIPTIONS USING OPEN AI
// ---------------------------------------------------
// Original author: Darren Gosbell, 
// https://darren.gosbell.com/2023/02/automatically-generating-measure-descriptions-for-power-bi-and-analysis-services-with-chatgpt-and-tabular-editor/
// ---------------------------------------------------


using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

// You need to signin to https://platform.openai.com/ and create an API key for your profile. Then, save the
// API key as an environment variable with the name below, or simply paste it in:
string apiKey = Environment.GetEnvironmentVariable("TE_OpenAI_APIKey");
// const string apiKey = "<YOUR API KEY HERE>";
const string uri = "https://api.openai.com/v1/completions";
const string question = "Explain the following calculation in a few sentences in simple business terms without using DAX function names:\n\n";

const int oneMinute = 60000; // the number of milliseconds in a minute
const int apiLimit = 20;     // a free account is limited to 20 calls per minute, change this if you have a paid account
const bool dontOverwrite = true; // this prevents existing descriptions from being overwritten

using (var client = new HttpClient()) {
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);

    int callCount = 0;
    
    // if any measures are currently selected add those 
    // to our collection
    List<Measure> myMeasures = new List<Measure>();
    myMeasures.AddRange( Selected.Measures );

    // if no measures were selected grab all of the
    // measures in the model
    if ( myMeasures.Count == 0)
    {
       myMeasures.AddRange(Model.Tables.Where(t => t.Measures.Count() > 0).SelectMany(t => t.Measures));
    }

        
    foreach ( var m in myMeasures)
    {
        // if we are not overwriting existing descriptions then skip to the 
        // next measure if this one is not an empty string
        if (dontOverwrite && !string.IsNullOrEmpty(m.Description)) {continue; }
        
        // Only uncomment the following when running from the command line or the script will 
        // show a popup after each measure
        //Info("Processing " + m.DaxObjectFullName) 
        //var body = new requestBody() { prompt = question + m.Expression   };
        var body = 
        "{ \"prompt\": " + JsonConvert.SerializeObject(question + m.Expression ) + 
            ",\"model\": \"gpt-3.5-turbo-instruct\" " +
            ",\"temperature\": 1 " +
            ",\"max_tokens\": 2048 " +
            ",\"stop\": \".\" }";

        var res = client.PostAsync(uri, new StringContent(body, Encoding.UTF8,"application/json"));
        res.Result.EnsureSuccessStatusCode();
        var result = res.Result.Content.ReadAsStringAsync().Result;
        var obj = JObject.Parse(result);
        var desc = obj["choices"][0]["text"].ToString().Trim();
        m.Description = desc + "\n=====\n" + m.Expression;
        
        callCount++; // increment the call count
        if ( callCount % apiLimit == 0) System.Threading.Thread.Sleep( oneMinute );
    
    }
}