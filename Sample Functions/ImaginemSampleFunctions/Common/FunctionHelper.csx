#r "System.IO"
#r "System.Runtime"
#r "System.Threading.Tasks"
#r "System.Configuration"
#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"

using System;
using System.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
public class PipelineHelper
{
    public delegate dynamic ProcessFunc(dynamic inputJson, string imageUrl, TraceWriter log);
    public static void Process(ProcessFunc func, string processorName, string inputMsg, TraceWriter log)
    {
        Exception exception = null;
        dynamic inputJson = JsonConvert.DeserializeObject(inputMsg);
        dynamic jobDefinition = inputJson.job_definition;
        string imageUrl = jobDefinition.input.image_url;
        dynamic jobOutput = "";

        try
        {
            jobOutput = func(inputJson, imageUrl, log);
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        PipelineHelper.Commit(inputJson, processorName, imageUrl, jobOutput, exception, log);
    }
    private static void Commit(dynamic inputJson, string jobName, string imageUrl, dynamic jobOutput, Exception exception, TraceWriter log)
    {
        log.Info($"start commit {jobName}");

        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
        CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
        CloudTable table = tableClient.GetTableReference("pipelinelogs");
        table.CreateIfNotExists();

        DynamicTableEntity logEntity = new DynamicTableEntity(inputJson.job_definition.batch_id.ToString(), inputJson.job_definition.id.ToString());
        logEntity.Properties.Add("image_url", EntityProperty.GeneratePropertyForString(imageUrl));
        try
        {
            dynamic jobDefinition = inputJson.job_definition;
            int processingStep = (int)jobDefinition.processing_step;
            int nextProcessingStep = processingStep + 1;
            string inputQueue = jobDefinition.processing_pipeline[processingStep];

            if (exception == null)
            {
                // adding the job data to the output json
                ((JObject)inputJson.job_output).Add(jobName, JObject.FromObject(jobOutput));
                logEntity.Properties.Add(inputQueue, EntityProperty.GeneratePropertyForString(string.Format("step {0}: success", processingStep)));
                logEntity.Properties.Add(jobName + "_output", EntityProperty.GeneratePropertyForString(JsonConvert.SerializeObject(jobOutput)));
            }
            else
            {
                // writting exception details to table storage
                logEntity.Properties.Add(inputQueue, EntityProperty.GeneratePropertyForString(string.Format("step {0}: failed", processingStep)));
                logEntity.Properties.Add(jobName + "_exception", EntityProperty.GeneratePropertyForString(
                    string.Format("{0} - {1}", exception.Message, exception.InnerException)));
            }
            //there is a next step in the pipeline
            if (jobDefinition.processing_pipeline.Count > nextProcessingStep)
            {
                string outputQueue = jobDefinition.processing_pipeline[nextProcessingStep];
                logEntity.Properties.Add(outputQueue, EntityProperty.GeneratePropertyForString(string.Format("step {0}: processing", nextProcessingStep)));
                inputJson.job_definition.processing_step = nextProcessingStep;

                log.Info($"next processing step {outputQueue}");
                CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                CloudQueue queue = queueClient.GetQueueReference(outputQueue);
                queue.CreateIfNotExists();
                string outputJson = JsonConvert.SerializeObject(inputJson);
                CloudQueueMessage message = new CloudQueueMessage(outputJson);
                queue.AddMessage(message);
                log.Info($"message added to queue {outputQueue}: {outputJson}");
            }
            else
            {
                logEntity.Properties.Add("job_output", EntityProperty.GeneratePropertyForString(JsonConvert.SerializeObject(inputJson)));
            }
            log.Info($"succesfully committed {jobName}");
        }
        catch (Exception ex)
        {
            log.Error($"Exception in {jobName}: {ex.Message} - {ex.InnerException}");
        }
        table.Execute(TableOperation.InsertOrMerge(logEntity));
    }
}

