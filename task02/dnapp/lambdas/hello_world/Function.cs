using System.Collections.Generic;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    public APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        //var responce body = new
        //{
        //    statusCode = 200,
        //    message = $"Path {request.Path} Method {request.HttpMethod}"
        //};
        //return new APIGatewayProxyResponse
        //{
        //    StatusCode = 200,
        //    Body = JsonSerializer.Serialize(responseBody),
        //    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        //};
        //if (request.HttpMethod == "GET" && request.Path == "/hello")
        //{
        //    var responseBody = new
        //    {
        //        statusCode = 200,
        //        message = "Hello from Lambda"
        //    };

        //    return new APIGatewayProxyResponse
        //    {
        //        StatusCode = 200,
        //        Body = JsonSerializer.Serialize(responseBody),
        //        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        //    };
        //}
        //else
        //{
        //    var errorResponse = new
        //    {
        //        statusCode = 400,
        //        message = $"Bad request syntax or unsupported method. Request path: {request.Resource}. HTTP method: {request.HttpMethod}"
        //    };

        //    return new APIGatewayProxyResponse
        //    {
        //        StatusCode = 400,
        //        Body = JsonSerializer.Serialize(errorResponse),
        //        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        //    };
        //}
        string httpMethod = request?.RequestContext.HttpMethod ?? "UNKNOWN";
        string path = request?.RequestContext.Path ?? "UNKNOWN";

        string serialiredRequest = JsonSerializer.Serialize(request);
        string serializedContext = JsonSerializer.Serialize(context);

        if (httpMethod == "GET" && path == "/hello")
        {
            var responseBody = new
            {
                StatusCode = 200,
                Message = "Hello from Lambda"
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
                StatusCode = 400,
                Message = $"Bad request syntax or unsupported method. Request path: {request.RequestContext.Path}. " +
                $"HTTP method: {request.RequestContext.HttpMethod}. " +
                $"Serialized request {serialiredRequest}" +
                $"Serialized context {serializedContext}"
            };

            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = JsonSerializer.Serialize(errorResponse),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }
}
