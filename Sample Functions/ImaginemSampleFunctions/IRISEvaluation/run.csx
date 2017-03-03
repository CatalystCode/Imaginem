#r "System.IO"
#r "System.Net.Http"
#r "System.Web"
#r "System.Runtime"
#r "System.Threading.Tasks"
#load "..\Common\FunctionHelper.csx"

using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Net.Http;
using System.Web;
using System.Runtime;
using System.Configuration;

private const string NAME = "iriseval";

private const string APIKey = "<YOUR_PREDICTION_KEY>";
private const string ProjectId = "<YOUR_PROJECT_ID>";
private const string IterationId = "<YOUR_ITERATION_ID>";

public static async Task Run(string inputMsg, TraceWriter log)
{
    dynamic inputJson = JsonConvert.DeserializeObject(inputMsg);

    var imageUrl = inputJson.job_definition.input.image_url.ToString();
    var result = await PostToEvaluationApiAsync(imageUrl, ProjectId, IterationId, log);

    dynamic outputJson = new { result = result };
    PipelineHelper.Commit(inputJson, NAME, outputJson, log);
}

private static async Task<string> PostToEvaluationApiAsync(string imageUrl, string projectId, string iterationId, TraceWriter log)
{
    try
    {
        var client = new HttpClient();
        var queryString = HttpUtility.ParseQueryString(string.Empty);

        // Request headers
        client.DefaultRequestHeaders.Add("Prediction-Key", APIKey);

        // Request parameters
        queryString["iterationId"] = iterationId;
        var uri = "https://customvisionppe.azure-api.net/v1.0/Prediction/" + projectId + "/url?" + queryString;

        var contentStr = "{\"Url\":\"" + imageUrl + "\"}";

        var content = new StringContent(contentStr);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await client.PostAsync(uri, content);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync(); ;
        }
        else
        {
            return response.ReasonPhrase;
        }
    }
    catch (Exception e)
    {
        log.Info(e.Message);
        log.Info(e.StackTrace);
        return e.Message;
    }
}