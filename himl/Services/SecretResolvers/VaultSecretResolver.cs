using himl.core.Interfaces;
using Microsoft.Extensions.Logging;

namespace himl.Services.SecretResolvers
{
    public class VaultSecretResolver : ISecretResolver
    {
        private readonly ILogger<VaultSecretResolver> _logger;

        public VaultSecretResolver(ILogger<VaultSecretResolver> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Supports(string secretType) => secretType == "vault";

        public Task<string> ResolveAsync(string secretType, IDictionary<string, string> parameters)
        {
            _logger.LogWarning("Vault secret resolver is not fully implemented (placeholder)");

            // Prefer explicit 'path', then 'key', then any first parameter value.
            try
            {
                if (parameters == null || parameters.Count == 0)
                {
                    _logger.LogWarning("No parameters provided for Vault secret resolution");
                    return Task.FromResult(string.Empty);
                }

                string? path = null;
                if (parameters.TryGetValue("path", out var p)) path = p;
                else if (parameters.TryGetValue("key", out var k)) path = k;
                else path = parameters.Values.FirstOrDefault();

                path ??= string.Empty;
                _logger.LogInformation("Resolving Vault secret placeholder for path: {Path}", path);
                return Task.FromResult($"vault-secret-from-{path}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in placeholder VaultSecretResolver");
                return Task.FromResult<string>(string.Empty);
            }
        }
    }
}
