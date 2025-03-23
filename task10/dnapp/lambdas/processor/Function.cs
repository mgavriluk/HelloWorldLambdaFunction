using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    private readonly AmazonDynamoDBClient _dynamoDbClient;
    private readonly string _tableName;
    private readonly HttpClient _httpClient;
    private const string WeatherApiUrl = "https://api.open-meteo.com/v1/forecast?latitude=52.52&longitude=13.41&current=temperature_2m,wind_speed_10m&hourly=temperature_2m,relative_humidity_2m,wind_speed_10m";

    public Function()
    {
        _dynamoDbClient = new AmazonDynamoDBClient();
        _httpClient = new HttpClient();
        _tableName = Environment.GetEnvironmentVariable("target_table");
        AWSSDKHandler.RegisterXRayForAllServices();
    }

    public async Task FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("FetchWeatherData");
        var response = await _httpClient.GetStringAsync(WeatherApiUrl);
        AWSXRayRecorder.Instance.EndSubsegment();

        var weatherData = JsonSerializer.Deserialize<WeatherData>(response);
        if (weatherData == null)
        {
            context.Logger.LogWarning("Failed to deserialize weather data.");
            return;
        }

        var weatherRecord = new WeatherRecord
        {
            Id = Guid.NewGuid().ToString(),
            Forecast = weatherData
        };

        AWSXRayRecorder.Instance.BeginSubsegment("SaveWeatherData");
        var putItemRequest = new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["id"] = new AttributeValue { S = weatherRecord.Id },
                ["forecast"] = new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["elevation"] = new AttributeValue { N = weatherRecord.Forecast.Elevation.ToString() },
                        ["generationtime_ms"] = new AttributeValue { N = weatherRecord.Forecast.GenerationTimeMs.ToString() },
                        ["hourly"] = new AttributeValue
                        {
                            M = new Dictionary<string, AttributeValue>
                            {
                                ["temperature_2m"] = new AttributeValue
                                {
                                    L = weatherRecord.Forecast.Hourly.Temperature2M.Select(t => new AttributeValue { N = t.ToString() }).ToList()
                                },
                                ["time"] = new AttributeValue
                                {
                                    L = weatherRecord.Forecast.Hourly.Time.Select(t => new AttributeValue { S = t }).ToList()
                                }
                            }
                        },
                        ["hourly_units"] = new AttributeValue
                        {
                            M = new Dictionary<string, AttributeValue>
                            {
                                ["temperature_2m"] = new AttributeValue { S = weatherRecord.Forecast.HourlyUnits.Temperature2M },
                                ["time"] = new AttributeValue { S = weatherRecord.Forecast.HourlyUnits.Time }
                            }
                        },
                        ["latitude"] = new AttributeValue { N = weatherRecord.Forecast.Latitude.ToString() },
                        ["longitude"] = new AttributeValue { N = weatherRecord.Forecast.Longitude.ToString() },
                        ["timezone"] = new AttributeValue { S = weatherRecord.Forecast.Timezone },
                        ["timezone_abbreviation"] = new AttributeValue { S = weatherRecord.Forecast.TimezoneAbbreviation },
                        ["utc_offset_seconds"] = new AttributeValue { N = weatherRecord.Forecast.UtcOffsetSeconds.ToString() }
                    }
                }
            }
        };

        try
        {
            await _dynamoDbClient.PutItemAsync(putItemRequest);
            context.Logger.LogInformation("Successfully saved record to table.");
        }
        catch (Exception ex)
        {
            context.Logger.LogInformation($"Error saving record to table: {ex.Message}");
            throw;
        }
        AWSXRayRecorder.Instance.EndSubsegment();
    }
}

public class WeatherRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("forecast")]
    public WeatherData Forecast { get; set; }
}

public class WeatherData
{
    [JsonPropertyName("elevation")]
    public double Elevation { get; set; }

    [JsonPropertyName("generationtime_ms")]
    public double GenerationTimeMs { get; set; }

    [JsonPropertyName("hourly")]
    public HourlyData Hourly { get; set; }

    [JsonPropertyName("hourly_units")]
    public HourlyUnits HourlyUnits { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; }

    [JsonPropertyName("timezone_abbreviation")]
    public string TimezoneAbbreviation { get; set; }

    [JsonPropertyName("utc_offset_seconds")]
    public int UtcOffsetSeconds { get; set; }
}

public class HourlyData
{
    [JsonPropertyName("temperature_2m")]
    public List<double> Temperature2M { get; set; }

    [JsonPropertyName("time")]
    public List<string> Time { get; set; }
}

public class HourlyUnits
{
    [JsonPropertyName("temperature_2m")]
    public string Temperature2M { get; set; }

    [JsonPropertyName("time")]
    public string Time { get; set; }
}