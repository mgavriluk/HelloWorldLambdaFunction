using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    private readonly AmazonDynamoDBClient _dynamoDbClient;
    private readonly string _tableName;

    public Function()
    {
        // Initialize DynamoDB client
        _dynamoDbClient = new AmazonDynamoDBClient();
        _tableName = Environment.GetEnvironmentVariable("target_table");
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation("FunctionHandler invoked.");
        context.Logger.LogInformation($"Serialized AmazonDynamoDBClient: {JsonSerializer.Serialize(_dynamoDbClient)}");

        try
        {
            // Log the incoming request body
            context.Logger.LogInformation($"Request Body: {request.Body}");

            // Deserialize the incoming request body
            var eventRequest = JsonSerializer.Deserialize<EventRequest>(request.Body);

            // Validate the request
            if (eventRequest == null)
            {
                context.Logger.LogWarning("Deserialization failed. EventRequest is null.");
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = JsonSerializer.Serialize(new { message = "Invalid request payload" })
                };
            }

            if (eventRequest.PrincipalId <= 0 || eventRequest.Content == null)
            {
                context.Logger.LogWarning($"Validation failed. PrincipalId: {eventRequest.PrincipalId}, Content: {eventRequest.Content}");
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = JsonSerializer.Serialize(new { message = "Invalid request payload" })
                };
            }

            // Log the validated request
            context.Logger.LogInformation($"Validated EventRequest: {JsonSerializer.Serialize(eventRequest)}");

            // Create the event object
            var eventToSave = new Event
            {
                Id = Guid.NewGuid().ToString(), // Generate UUID v4
                PrincipalId = eventRequest.PrincipalId,
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), // ISO 8601 format
                Body = eventRequest.Content
            };

            // Log the event to be saved
            context.Logger.LogInformation($"Event to Save: {JsonSerializer.Serialize(eventToSave)}");

            // Save the event to DynamoDB
            await SaveEventToDynamoDB(eventToSave, context);

            // Log successful save
            context.Logger.LogInformation("Event successfully saved to DynamoDB.");

            // Return the created event as a response
            var response = new APIGatewayProxyResponse
            {
                StatusCode = 201,
                Body = JsonSerializer.Serialize(new Response
                {
                    StatusCode = 201,
                    Event = eventToSave
                }),
                Headers = new Dictionary<string, string>()
                {
                    { "Content-Type", "application/json" }
                }
            };

            // Log the response
            context.Logger.LogInformation($"Response: {JsonSerializer.Serialize(response)}");

            return response;
        }
        catch (AmazonDynamoDBException dbEx)
        {
            // Log DynamoDB-specific errors
            context.Logger.LogError($"DynamoDB Error: {dbEx.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { message = "DynamoDB error occurred" })
            };
        }
        catch (Exception ex)
        {
            // Log general errors
            context.Logger.LogError($"General Error: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { message = "Internal server error" })
            };
        }
    }

    private async Task SaveEventToDynamoDB(Event eventToSave, ILambdaContext context)
    {
        context.Logger.LogInformation("SaveEventToDynamoDB invoked.");

        try
        {
            var request = new PutItemRequest
            {
                TableName = _tableName, // DynamoDB table name
                Item = new Dictionary<string, AttributeValue>
                {
                    { "id", new AttributeValue { S = eventToSave.Id } },
                    { "principalId", new AttributeValue { N = eventToSave.PrincipalId.ToString() } },
                    { "createdAt", new AttributeValue { S = eventToSave.CreatedAt } },
                    { "body", new AttributeValue { M = ConvertToAttributeValueMap(eventToSave.Body) } }
                }
            };

            // Log the PutItemRequest
            context.Logger.LogInformation($"DynamoDB PutItemRequest: {JsonSerializer.Serialize(request)}");

            var response = await _dynamoDbClient.PutItemAsync(request);

            // Log the DynamoDB response
            context.Logger.LogInformation($"DynamoDB Response: {response.HttpStatusCode}");
        }
        catch (AmazonDynamoDBException dbEx)
        {
            // Log DynamoDB-specific errors
            context.Logger.LogError($"DynamoDB Error: {dbEx.Message}");
            throw;
        }
        catch (Exception ex)
        {
            // Log general errors
            context.Logger.LogError($"Error saving event to DynamoDB: {ex.Message}");
            throw;
        }
    }

    private Dictionary<string, AttributeValue> ConvertToAttributeValueMap(Dictionary<string, string> dictionary)
    {
        var attributeValueMap = new Dictionary<string, AttributeValue>();
        foreach (var kvp in dictionary)
        {
            attributeValueMap[kvp.Key] = new AttributeValue { S = kvp.Value };
        }
        return attributeValueMap;
    }
}

public class EventRequest
{
    [JsonPropertyName("principalId")]
    public int PrincipalId { get; set; }

    [JsonPropertyName("content")]
    public Dictionary<string, string> Content { get; set; }
}

public class Event
{
    [JsonPropertyName("id")]
    public string Id { get; set; } // Partition Key (String)

    [JsonPropertyName("principalId")]
    public int PrincipalId { get; set; } // Number

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } // ISO 8601 String

    [JsonPropertyName("body")]
    public Dictionary<string, string> Body { get; set; } // JSON Object
}

public class Response
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("event")]
    public Event? Event { get; set; }
}