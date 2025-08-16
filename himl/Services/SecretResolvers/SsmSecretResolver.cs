using himl.core.Interfaces;
using Microsoft.Extensions.Logging;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

using himl.Extensions;

namespace himl.Services.SecretResolvers
{
    public class SsmSecretResolver : ISecretResolver
    {
        private readonly ILogger<SsmSecretResolver> _logger;

        public SsmSecretResolver(ILogger<SsmSecretResolver> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Supports(string secretType) => secretType == "ssm";

        public async Task<string> ResolveAsync(string secretType, IDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("path", out var path))
            {
                throw new ArgumentException("SSM secret requires 'path' parameter");
            }

            var awsProfile = parameters.GetValueOrDefault("aws_profile");
            var regionName = parameters.GetValueOrDefault("region_name", "us-east-1");

            try
            {
                _logger.LogInformation("Resolving SSM parameter: {Path} in region {Region}", path, regionName);

                var config = new AmazonSimpleSystemsManagementConfig { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(regionName) };

                if (!string.IsNullOrEmpty(awsProfile))
                {
                    _logger.LogDebug("Using AWS profile: {Profile}", awsProfile);
                }

                using var ssmClient = new AmazonSimpleSystemsManagementClient(config);

                var request = new GetParameterRequest
                {
                    Name = path,
                    WithDecryption = true
                };

                var response = await ssmClient.GetParameterAsync(request);
                return response.Parameter.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve SSM parameter: {Path}", path);
                throw new InvalidOperationException($"Failed to resolve SSM parameter '{path}': {ex.Message}", ex);
            }
        }
    }
}
