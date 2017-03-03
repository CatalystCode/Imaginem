using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imaginem_Cli
{
    class Program
    {
        static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Imaginem-Cli usage:");
                Console.WriteLine("=====================\n");
                PrintTestUsage();
                PrintPipelineUsage();
                PrintClearUsage();
                return;
            }
            else 
            {
                switch (args[0].ToLower())
                {
                    case "test":
                        if (args.Length != 3)
                        {
                            PrintTestUsage();
                        }
                        else
                        {
                            RunTest(args[1], args[2]);
                        }
                        break;
                    case "pipeline":
                        if (args.Length != 2)
                        {
                            PrintPipelineUsage();
                        }
                        else
                        {
                            RunPipeline(args[1]);
                        }
                        break;
                    case "clear":
                        if (args.Length != 2)
                        {
                            PrintClearUsage();
                        }
                        else
                        {
                            RunClear(args[1]);
                        }
                        break;
                }
            }
        }

        private static void PrintTestUsage()
        {
            Console.WriteLine("Test function");
            Console.WriteLine("     Imaginem-Cli.exe test <FunctionName> <InputQueueName>");
        }

        private static void PrintPipelineUsage()
        {
            Console.WriteLine("Test pipeline");
            Console.WriteLine("     Imaginem-Cli.exe pipeline <pipelineTest>");
        }
        private static void PrintClearUsage()
        {
            Console.WriteLine("clear queues");
            Console.WriteLine("     Imaginem-Cli.exe clear <pipelineTest>");
        }
        static void RunTest(string functionFolder, string queueName)
        {
            string testJsonPath = string.Format("{0}\\Imaginem-Functions\\{1}\\test.json", ConfigurationManager.AppSettings["ImaginemRoot"], functionFolder);
            PostJson(queueName, File.ReadAllText(testJsonPath));
        }

        static void RunPipeline(string testName)
        {
            string testJsonPath = string.Format("{0}\\Tests\\{1}.json", ConfigurationManager.AppSettings["ImaginemRoot"], testName);
            Console.WriteLine("running test " + testJsonPath);

            string json = File.ReadAllText(testJsonPath);
            json = json.Replace("<<BATCHID>>", Guid.NewGuid().ToString());
            if (json.IndexOf("<<URL>>") == -1)
            {
                json = json.Replace("<<JOBID>>", Guid.NewGuid().ToString());
                dynamic inputJson = JsonConvert.DeserializeObject(json);
                dynamic jobDefinition = inputJson.job_definition;
                string inputQueueName = jobDefinition.processing_pipeline[0];
                PostJson(inputQueueName, json);
            }
            else
            {
                RunPipelineBulk(testName);
            }
        }
        static void RunPipelineBulk(string testName)
        {
            string testJsonPath = string.Format("{0}\\Tests\\{1}.json", ConfigurationManager.AppSettings["ImaginemRoot"], testName);
            Console.WriteLine("running test " + testJsonPath);

            string jsonTemplate = File.ReadAllText(testJsonPath);
            jsonTemplate = jsonTemplate.Replace("<<BATCHID>>", Guid.NewGuid().ToString());

            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            CloudBlobContainer container = blobClient.GetContainerReference("inputimages");
            foreach (IListBlobItem item in container.ListBlobs(null, true))
            {
                if (item.GetType() == typeof(CloudBlockBlob))
                {
                    string json = jsonTemplate.Replace("<<JOBID>>", Guid.NewGuid().ToString());
                    json = json.Replace("<<URL>>", item.StorageUri.PrimaryUri.AbsoluteUri);
                    dynamic inputJson = JsonConvert.DeserializeObject(json);
                    dynamic jobDefinition = inputJson.job_definition;

                    string inputQueueName = jobDefinition.processing_pipeline[0];
                    PostJson(inputQueueName, json);
                }
            }
        }
        static void RunClear(string testName)
        {
            string testJsonPath = string.Format("{0}\\Tests\\{1}.json", ConfigurationManager.AppSettings["ImaginemRoot"], testName);
            Console.WriteLine("running test " + testJsonPath);

            string json = File.ReadAllText(testJsonPath);
            dynamic inputJson = JsonConvert.DeserializeObject(json);
            dynamic jobDefinition = inputJson.job_definition;

            foreach (var queueName in jobDefinition.processing_pipeline)
            {
                CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                CloudQueue queue = queueClient.GetQueueReference(queueName.ToString());
                if (queue.Exists())
                {
                    Console.WriteLine("Clear " + queueName.ToString());
                    queue.Clear();
                }
            }
        }
        private static void PostJson(string queueName, string json)
        {
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue queue = queueClient.GetQueueReference(queueName);
            queue.CreateIfNotExists();

            Console.WriteLine("Adding message to " + queueName + "...");
            Console.WriteLine(json);

            CloudQueueMessage message = new CloudQueueMessage(json);
            queue.AddMessage(message);
        }
    }
}
