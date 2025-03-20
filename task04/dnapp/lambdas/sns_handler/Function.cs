using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    public async Task FunctionHandler(SNSEvent snsEvent, ILambdaContext context)
    {
        context.Logger.LogInformation($"Received {snsEvent.Records.Count} SNS messages.");

        foreach (var record in snsEvent.Records)
        {
            context.Logger.LogInformation($"Message ID: {record.Sns.MessageId}");
            context.Logger.LogInformation($"Message Subject: {record.Sns.Subject}");
            context.Logger.LogInformation($"Message Body: {record.Sns.Message}");
        }

        await Task.CompletedTask;
    }
}
