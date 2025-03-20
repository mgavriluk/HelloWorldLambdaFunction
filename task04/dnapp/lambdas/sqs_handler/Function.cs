using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        context.Logger.LogInformation($"Received {sqsEvent.Records.Count} messages from SQS.");

        foreach (var record in sqsEvent.Records)
        {
            context.Logger.LogInformation($"Message ID: {record.MessageId}");
            context.Logger.LogInformation($"Message Body: {record.Body}");
        }

        await Task.CompletedTask;
    }
}
