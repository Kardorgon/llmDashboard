using Microsoft.Extensions.Options;
using server.Configuration;

namespace server.Services.Llm;

public sealed class LlmClientRouter(
    IOptionsMonitor<LlmOptions> optionsMonitor,
    GeminiLlmClient geminiLlmClient,
    OllamaLlmClient ollamaLlmClient,
    MockLlmClient mockLlmClient,
    ILogger<LlmClientRouter> logger
)
{
    public ILlmClient Resolve()
    {
        var options = optionsMonitor.CurrentValue;
        var provider = options.Provider.Trim().ToLowerInvariant();

        if (provider is "gemini")
        {
            if (string.IsNullOrWhiteSpace(options.Gemini.ApiKey))
            {
                logger.LogWarning("Gemini provider selected without API key. Falling back to Mock.");
                return mockLlmClient;
            }

            return geminiLlmClient;
        }

        if (provider is "ollama")
        {
            return ollamaLlmClient;
        }

        return mockLlmClient;
    }
}
