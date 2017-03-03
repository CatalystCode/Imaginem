using System;

private const string NAME = "opencvprocess";

public static void Run(string inputMsg, TraceWriter log)
{
    log.Info($"C# Queue trigger function processed: {inputMsg}");
}