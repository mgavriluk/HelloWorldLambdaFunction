using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    private readonly AmazonDynamoDBClient _dynamoDbClient;
    private readonly string _tableName;

    public Function()
    {
        _dynamoDbClient = new AmazonDynamoDBClient();
        _tableName = Environment.GetEnvironmentVariable("target_table");
    }

    public async Task FunctionHandler(DynamoDBEvent dynamoEvent, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing {dynamoEvent.Records.Count} DynamoDB event(s)...");

        foreach (var record in dynamoEvent.Records)
        {
            context.Logger.LogLine($"Processing record with EventName: {record.EventName}");

            if (record.EventName == "INSERT")
            {
                await HandleInsert(record, context);
            }
            else if (record.EventName == "MODIFY")
            {
                await HandleModify(record, context);
            }
            else
            {
                context.Logger.LogLine($"Skipping unsupported event type: {record.EventName}");
            }
        }
    }

    private async Task HandleInsert(DynamoDBEvent.DynamodbStreamRecord record, ILambdaContext context)
    {
        var newImage = record.Dynamodb.NewImage;

        if (newImage == null || !newImage.ContainsKey("key") || !newImage.ContainsKey("value"))
        {
            context.Logger.LogWarning("Missing 'key' or 'value' in INSERT event.");
            return;
        }

        string itemKey = newImage["key"].S;
        int value = int.Parse(newImage["value"].N);

        var auditItem = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = Guid.NewGuid().ToString() },
            ["itemKey"] = new AttributeValue { S = itemKey },
            ["modificationTime"] = new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
            ["newValue"] = new AttributeValue
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["key"] = new AttributeValue { S = itemKey },
                    ["value"] = new AttributeValue { N = value.ToString() }
                }
            }
        };

        await SaveToAuditTable(auditItem, context);
    }

    private async Task HandleModify(DynamoDBEvent.DynamodbStreamRecord record, ILambdaContext context)
    {
        var newImage = record.Dynamodb.NewImage;
        var oldImage = record.Dynamodb.OldImage;

        if (newImage == null || oldImage == null || !newImage.ContainsKey("key") || !newImage.ContainsKey("value"))
        {
            context.Logger.LogWarning("Missing 'key' or 'value' in MODIFY event.");
            return;
        }

        string itemKey = newImage["key"].S;
        int newValue = int.Parse(newImage["value"].N);
        int oldValue = int.Parse(oldImage["value"].N);

        if (newValue == oldValue)
        {
            context.Logger.LogInformation($"No value change detected for {itemKey}, skipping audit log.");
            return;
        }

        var auditItem = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = Guid.NewGuid().ToString() },
            ["itemKey"] = new AttributeValue { S = itemKey },
            ["modificationTime"] = new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
            ["updatedAttribute"] = new AttributeValue { S = "value" },
            ["newValue"] = new AttributeValue { N = newValue.ToString() },
            ["oldValue"] = new AttributeValue { N = oldValue.ToString() }
        };


        await SaveToAuditTable(auditItem, context);
    }

    private async Task SaveToAuditTable(Dictionary<string, AttributeValue> auditItem, ILambdaContext context)
    {
        try
        {
            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = auditItem
            };

            await _dynamoDbClient.PutItemAsync(request);
            context.Logger.LogInformation("Successfully saved record to Audit table.");
        }
        catch (Exception ex)
        {
            context.Logger.LogInformation($"Error saving record to Audit table: {ex.Message}");
            throw;
        }
    }
}