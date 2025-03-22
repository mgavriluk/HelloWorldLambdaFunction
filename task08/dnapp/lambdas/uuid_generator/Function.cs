using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    private readonly IAmazonS3 _s3Client;
    private readonly string BucketName;

    public Function()
    {
        _s3Client = new AmazonS3Client();
        BucketName = Environment.GetEnvironmentVariable("target_bucket");
    }

    public async Task FunctionHandler(object input, ILambdaContext context)
    {
        var uuids = GenerateRandomUuids(10);

        // Create the file content
        var fileContent = new
        {
            ids = uuids
        };

        // Serialize the content to JSON
        var jsonContent = JsonSerializer.Serialize(fileContent);

        // Generate the file name based on the current timestamp
        var fileName = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}";

        // Save the file to S3
        await SaveToS3Async(fileName, jsonContent);
    }

    private List<string> GenerateRandomUuids(int count)
    {
        var uuids = new List<string>();
        for (int i = 0; i < count; i++)
        {
            uuids.Add(Guid.NewGuid().ToString());
        }
        return uuids;
    }

    private async Task SaveToS3Async(string fileName, string content)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = BucketName,
            Key = fileName,
            ContentBody = content,
            ContentType = "application/json"
        };

        await _s3Client.PutObjectAsync(putRequest);
    }
}
