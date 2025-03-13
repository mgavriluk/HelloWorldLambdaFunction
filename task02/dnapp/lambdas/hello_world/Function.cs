using System.Collections.Generic;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    public APIGatewayProxyResponse FunctionHandler(Dictionary<string, object> request, ILambdaContext context)
    {
        string httpMethod = ExtractHttpMethod(request);
        string path = ExtractPath(request);

        if (httpMethod == "GET" && path == "/hello")
        {
            var responseBody = new
            {
                statusCode = 200,
                message = "Hello from Lambda"
            };

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(responseBody),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
        else
        {
            var errorResponse = new
            {
                statusCode = 400,
                message = $"Bad request syntax or unsupported method. Request path: {path}. HTTP method: {httpMethod}"
            };

            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = JsonSerializer.Serialize(errorResponse),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }

    private string ExtractHttpMethod(Dictionary<string, object> request)
    {
        try
        {
            if (request.TryGetValue("requestContext", out var requestContextObj) && requestContextObj is JsonElement requestContext)
            {
                if (requestContext.TryGetProperty("http", out var http) && http.TryGetProperty("method", out var method))
                {
                    return method.GetString() ?? "UNKNOWN";
                }
            }
        }
        catch (Exception) { }
        return "UNKNOWN";
    }

    private string ExtractPath(Dictionary<string, object> request)
    {
        try
        {
            if (request.TryGetValue("requestContext", out var requestContextObj) && requestContextObj is JsonElement requestContext)
            {
                if (requestContext.TryGetProperty("http", out var http) && http.TryGetProperty("path", out var path))
                {
                    return path.GetString() ?? "UNKNOWN";
                }
            }
        }
        catch (Exception) { }
        return "UNKNOWN";
    }
}
