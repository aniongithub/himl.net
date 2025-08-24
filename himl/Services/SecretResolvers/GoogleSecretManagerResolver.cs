using himl.core.Interfaces;
using Microsoft.Extensions.Logging;
using Google.Cloud.SecretManager.V1;
using himl.Extensions;

namespace himl.Services.SecretResolvers
{
    public class GoogleSecretManagerResolver : ISecretResolver
    {
        private readonly ILogger<GoogleSecretManagerResolver> _logger;

        public GoogleSecretManagerResolver(ILogger<GoogleSecretManagerResolver> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Supports(string secretType) => 
            secretType == "gcp" || secretType == "gsm" || secretType == "google";

        public async Task<string> ResolveAsync(string secretType, IDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("project_id", out var projectId))
            {
                throw new ArgumentException("Google Secret Manager requires 'project_id' parameter");
            }

            if (!parameters.TryGetValue("secret_id", out var secretId))
            {
                throw new ArgumentException("Google Secret Manager requires 'secret_id' parameter");
            }

            var version = parameters.GetValueOrDefault("version", "latest");
            var credentialsPath = parameters.GetValueOrDefault("credentials_path");

            try
            {
                _logger.LogInformation("Resolving Google Secret Manager secret: {ProjectId}/{SecretId}/{Version}", 
                    projectId, secretId, version);

                SecretManagerServiceClient client;
                
                if (!string.IsNullOrEmpty(credentialsPath))
                {
                    _logger.LogDebug("Using credentials file: {CredentialsPath}", credentialsPath);
                    // Set the GOOGLE_APPLICATION_CREDENTIALS environment variable
                    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);
                    client = SecretManagerServiceClient.Create();
                }
                else
                {
                    _logger.LogDebug("Using default application credentials");
                    client = SecretManagerServiceClient.Create();
                }

                var secretVersionName = SecretVersionName.FromProjectSecretSecretVersion(
                    projectId, secretId, version);

                var response = await client.AccessSecretVersionAsync(secretVersionName);
                var secretValue = response.Payload.Data.ToStringUtf8();

                _logger.LogDebug("Successfully resolved Google Secret Manager secret: {ProjectId}/{SecretId}", 
                    projectId, secretId);

                return secretValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve Google Secret Manager secret: {ProjectId}/{SecretId}/{Version}", 
                    projectId, secretId, version);
                throw new InvalidOperationException(
                    $"Failed to resolve Google Secret Manager secret '{projectId}/{secretId}/{version}': {ex.Message}", ex);
            }
        }
    }
}
