using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    private readonly AmazonDynamoDBClient _dynamoDbClient;
    private readonly AmazonCognitoIdentityProviderClient _cognitoClient;
    private readonly string _userPoolId;
    private readonly string _tablesTable;
    private readonly string _reservationsTable;

    public Function()
    {
        _dynamoDbClient = new AmazonDynamoDBClient();
        _cognitoClient = new AmazonCognitoIdentityProviderClient();
        _userPoolId = Environment.GetEnvironmentVariable("cup_id");
        _tablesTable = Environment.GetEnvironmentVariable("tables_table");
        _reservationsTable = Environment.GetEnvironmentVariable("reservations_table");
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(Dictionary<string, object> request, ILambdaContext context)
    {
        context.Logger.LogInformation($"Request: {JsonSerializer.Serialize(request)}");
        string httpMethod = request["httpMethod"].ToString();
        string path = request["path"].ToString();
        string body = request.ContainsKey("body") ? request["body"]?.ToString() : "{}";

        try
        {
            switch (httpMethod)
            {
                case "POST" when path == "/signup":
                    return await SignUp(JsonSerializer.Deserialize<Dictionary<string, string>>(body), context);
                case "POST" when path == "/signin":
                    return await SignIn(JsonSerializer.Deserialize<Dictionary<string, string>>(body), context);
                case "GET" when path == "/tables":
                    return await GetTables(context);
                case "POST" when path == "/tables":
                    return await CreateTable(JsonSerializer.Deserialize<Dictionary<string, object>>(body), context);
                case "GET" when path.StartsWith("/tables/"):
                    return await GetTableById(path, context);
                case "POST" when path == "/reservations":
                    return await CreateReservation(JsonSerializer.Deserialize<Dictionary<string, object>>(body), context);
                case "GET" when path == "/reservations":
                    return await GetReservations(context);
                default:
                    return AddCorsHeaders(new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest, Body = "Invalid request." });
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return AddCorsHeaders(new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError, Body = "Server error." });
        }
    }

    private async Task<APIGatewayProxyResponse> SignUp(Dictionary<string, string> requestBody, ILambdaContext context)
    {
        string email = requestBody["email"];
        string password = requestBody["password"];
        string firstName = requestBody["firstName"];
        string lastName = requestBody["lastName"];

        if (!IsValidEmail(email))
        {
            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = "Invalid email address format."
            });
        }

        if (!IsValidPassword(password))
        {
            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = "Password must be at least 12 characters long and contain letters, numbers, and one of $%^*-_."
            });
        }

        try
        {
            var signUpRequest = new AdminCreateUserRequest
            {
                UserPoolId = _userPoolId,
                Username = email,
                TemporaryPassword = password,
                UserAttributes = new List<AttributeType>
                {
                    new() { Name = "email", Value = email },
                    new() { Name = "given_name", Value = firstName },
                    new() { Name = "family_name", Value = lastName },
                    new() { Name = "email_verified", Value = "true" }
                },
                MessageAction = MessageActionType.SUPPRESS
            };

            await _cognitoClient.AdminCreateUserAsync(signUpRequest);

            var setPasswordRequest = new AdminSetUserPasswordRequest
            {
                UserPoolId = _userPoolId,
                Username = email,
                Password = password,
                Permanent = true
            };

            await _cognitoClient.AdminSetUserPasswordAsync(setPasswordRequest);

            //await _cognitoClient.AdminConfirmSignUpAsync(new AdminConfirmSignUpRequest
            //{
            //    Username = email,
            //    UserPoolId = _userPoolId
            //});

            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = "Sign-up successful. User is confirmed."
            });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Sign-up failed: {ex.Message}");
            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Body = $"Sign-up failed: {ex.Message}"
            });
        }
    }


    private async Task<APIGatewayProxyResponse> SignIn(Dictionary<string, string> requestBody, ILambdaContext context)
    {
        string clientId = Environment.GetEnvironmentVariable("cup_client_id");

        context.Logger.LogInformation($"ClientId: {clientId}");

        var authRequest = new InitiateAuthRequest
        {
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            ClientId = clientId,
            AuthParameters = new Dictionary<string, string>
            {
                { "USERNAME", requestBody["email"] },
                { "PASSWORD", requestBody["password"] }
            }
        };

        try
        {
            var authResponse = await _cognitoClient.InitiateAuthAsync(authRequest);

            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonSerializer.Serialize(new
                {
                    idToken = authResponse.AuthenticationResult.IdToken
                })
            });
        }
        catch (NotAuthorizedException ex)
        {
            context.Logger.LogError($"Sign-in failed: {ex.Message}");
            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = "Invalid email or password."
            });
        }
        catch (UserNotFoundException ex)
        {
            context.Logger.LogError($"Sign-in failed: {ex.Message}");
            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = "User does not exist."
            });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Sign-in failed: {ex.Message}");
            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Body = "Sign-in failed due to a server error."
            });
        }
    }

    private async Task<APIGatewayProxyResponse> GetTables(ILambdaContext context)
    {
        var table = Table.LoadTable(_dynamoDbClient, _tablesTable);
        var scanResults = await table.Scan(new ScanFilter()).GetRemainingAsync();

        var tables = scanResults.Select(item => new
        {
            id = item["id"].AsInt(),
            number = item["number"].AsInt(),
            places = item["places"].AsInt(),
            isVip = item["isVip"].AsBoolean(),
            minOrder = item["minOrder"].AsInt()
        }).ToList();

        context.Logger.LogInformation($"Tables: {JsonSerializer.Serialize(tables)}");

        return AddCorsHeaders(new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = JsonSerializer.Serialize(new { tables })
        });
    }

    private async Task<APIGatewayProxyResponse> CreateTable(Dictionary<string, object> requestBody, ILambdaContext context)
    {
        context.Logger.LogInformation($"Request body: {JsonSerializer.Serialize(requestBody)}");

        var putItemRequest = new PutItemRequest
        {
            TableName = _tablesTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["id"] = new AttributeValue { S = requestBody["id"] is JsonElement idElem ? idElem.GetInt32().ToString() : requestBody["id"].ToString() },
                ["number"] = new AttributeValue { N = requestBody["number"] is JsonElement numElem ? numElem.GetInt32().ToString() : requestBody["number"].ToString() },
                ["places"] = new AttributeValue { N = requestBody["places"] is JsonElement placesElem ? placesElem.GetInt32().ToString() : requestBody["places"].ToString() },
                ["isVip"] = new AttributeValue { BOOL = requestBody["isVip"] is JsonElement vipElem ? vipElem.GetBoolean() : (bool)requestBody["isVip"] }
            }
        };

        if (requestBody.ContainsKey("minOrder"))
        {
            putItemRequest.Item["minOrder"] = new AttributeValue { N = requestBody["minOrder"] is JsonElement minOrderElem ? minOrderElem.GetInt32().ToString() : requestBody["minOrder"].ToString() };
        }

        await _dynamoDbClient.PutItemAsync(putItemRequest);

        return AddCorsHeaders(new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = JsonSerializer.Serialize(new { id = requestBody["id"] })
        });
    }


    private async Task<APIGatewayProxyResponse> GetTableById(string path, ILambdaContext context)
    {
        context.Logger.LogInformation($"Path: {path}");

        string tableId = path.Split('/')[2];
        var table = Table.LoadTable(_dynamoDbClient, _tablesTable);
        var result = await table.GetItemAsync(tableId);

        if (result == null)
        {
            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.NotFound,
                Body = JsonSerializer.Serialize(new { error = "Table not found" })
            });
        }

        using var doc = JsonDocument.Parse(result.ToJson());
        var jsonObj = doc.RootElement.EnumerateObject()
            .Select(prop => new KeyValuePair<string, object>(
                prop.Name,
                prop.Name == "id" && prop.Value.ValueKind == JsonValueKind.String
                    ? JsonSerializer.Deserialize<object>(prop.Value.GetString()!)
                    : prop.Value
            ))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = JsonSerializer.Serialize(jsonObj)
        };
    }

    private async Task<APIGatewayProxyResponse> CreateReservation(Dictionary<string, object> requestBody, ILambdaContext context)
    {
        context.Logger.LogInformation($"Request body: {JsonSerializer.Serialize(requestBody)}");

        var reservationId = Guid.NewGuid().ToString();
        var tableNumber = requestBody["tableNumber"] is JsonElement tableElem ? tableElem.GetInt32() : Convert.ToInt32(requestBody["tableNumber"]);
        var date = requestBody["date"].ToString();
        var slotTimeStart = requestBody["slotTimeStart"].ToString();
        var slotTimeEnd = requestBody["slotTimeEnd"].ToString();

        var scanTableRequest = new ScanRequest
        {
            TableName = _tablesTable,
            FilterExpression = "#num = :tableNum",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#num"] = "number"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":tableNum"] = new AttributeValue { N = tableNumber.ToString() }
            }
        };

        var tableScanResponse = await _dynamoDbClient.ScanAsync(scanTableRequest);

        if (tableScanResponse.Items.Count == 0)
        {
            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = JsonSerializer.Serialize(new { error = "Table does not exist." })
            });
        }

        context.Logger.LogInformation($"tableScanResponse: {JsonSerializer.Serialize(tableScanResponse)}");

        var scanReservationsRequest = new ScanRequest
        {
            TableName = _reservationsTable,
            FilterExpression = "tableNumber = :tableNum AND #date = :date AND slotTimeStart <= :endTime AND slotTimeEnd >= :startTime",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#date"] = "date"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":tableNum"] = new AttributeValue { N = tableNumber.ToString() },
                [":date"] = new AttributeValue { S = date },
                [":startTime"] = new AttributeValue { S = slotTimeStart },
                [":endTime"] = new AttributeValue { S = slotTimeEnd }
            }
        };

        var scanReservationsResponse = await _dynamoDbClient.ScanAsync(scanReservationsRequest);

        context.Logger.LogInformation($"scanReservationsResponse: {JsonSerializer.Serialize(scanReservationsResponse)}");
        if (scanReservationsResponse.Items.Count > 0)
        {
            return AddCorsHeaders(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = JsonSerializer.Serialize(new { error = "Time slot is already booked." })
            });
        }

        var putItemRequest = new PutItemRequest
        {
            TableName = _reservationsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["id"] = new AttributeValue { S = reservationId },
                ["tableNumber"] = new AttributeValue { N = tableNumber.ToString() },
                ["clientName"] = new AttributeValue { S = requestBody["clientName"].ToString() },
                ["phoneNumber"] = new AttributeValue { S = requestBody["phoneNumber"].ToString() },
                ["date"] = new AttributeValue { S = date },
                ["slotTimeStart"] = new AttributeValue { S = slotTimeStart },
                ["slotTimeEnd"] = new AttributeValue { S = slotTimeEnd }
            }
        };

        await _dynamoDbClient.PutItemAsync(putItemRequest);

        return AddCorsHeaders(new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = JsonSerializer.Serialize(new { reservationId })
        });
    }


    private async Task<APIGatewayProxyResponse> GetReservations(ILambdaContext context)
    {
        var reservationTable = Table.LoadTable(_dynamoDbClient, _reservationsTable);
        var scanResults = await reservationTable.Scan(new ScanFilter()).GetRemainingAsync();

        var reservations = scanResults.Select(item => new
        {
            tableNumber = item["tableNumber"].AsInt(),
            clientName = item["clientName"].AsString(),
            phoneNumber = item["phoneNumber"].AsString(),
            date = item["date"].AsString(),
            slotTimeStart = item["slotTimeStart"].AsString(),
            slotTimeEnd = item["slotTimeEnd"].AsString()
        }).ToList();

        context.Logger.LogInformation($"Reservations: {JsonSerializer.Serialize(reservations)}");

        return AddCorsHeaders(new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = JsonSerializer.Serialize(new { reservations })
        });
    }

    private bool IsValidEmail(string email)
    {
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    private bool IsValidPassword(string password)
    {
        return Regex.IsMatch(password, @"^(?=.*[A-Za-z])(?=.*\d)(?=.*[$%^*\-_]).{12,}$");
    }

    private APIGatewayProxyResponse AddCorsHeaders(APIGatewayProxyResponse response)
    {
        response.Headers = new Dictionary<string, string>
        {
            { "Access-Control-Allow-Headers", "Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token" },
            { "Access-Control-Allow-Origin", "*" },
            { "Access-Control-Allow-Methods", "*" }
        };
        return response;
    }
}
