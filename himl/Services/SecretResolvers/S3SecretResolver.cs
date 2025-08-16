using himl.core.Interfaces;
using Microsoft.Extensions.Logging;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text;

using himl.Extensions;

namespace himl.Services.SecretResolvers
{
    public class S3SecretResolver : ISecretResolver
    {
        private readonly ILogger<S3SecretResolver> _logger;

        public S3SecretResolver(ILogger<S3SecretResolver> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Supports(string secretType) => secretType == "s3";

        public async Task<string> ResolveAsync(string secretType, IDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("bucket", out var bucket))
            {
                throw new ArgumentException("S3 secret requires 'bucket' parameter");
            }

            if (!parameters.TryGetValue("path", out var path))
            {
                throw new ArgumentException("S3 secret requires 'path' parameter");
            }

            var awsProfile = parameters.GetValueOrDefault("aws_profile");
            var regionName = parameters.GetValueOrDefault("region_name", "us-east-1");
            var base64Encode = Convert.ToBoolean(parameters.GetValueOrDefault("base64encode") ?? "false");

            try
            {
                _logger.LogInformation("Resolving S3 object: s3://{Bucket}/{Path} in region {Region}", bucket, path, regionName);

                var config = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(regionName) };

                if (!string.IsNullOrEmpty(awsProfile))
                {
                    _logger.LogDebug("Using AWS profile: {Profile}", awsProfile);
                }

                using var s3Client = new AmazonS3Client(config);

                var request = new GetObjectRequest
                {
                    BucketName = bucket,
                    Key = path
                };

                using var response = await s3Client.GetObjectAsync(request);
                using var reader = new StreamReader(response.ResponseStream);
                var content = await reader.ReadToEndAsync();

                if (base64Encode)
                {
                    var bytes = Encoding.UTF8.GetBytes(content);
                    return Convert.ToBase64String(bytes);
                }

                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve S3 object: s3://{Bucket}/{Path}", bucket, path);
                throw new InvalidOperationException($"Failed to resolve S3 object 's3://{bucket}/{path}': {ex.Message}", ex);
            }
        }
    }
}
