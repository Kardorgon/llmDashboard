namespace server.Configuration;

public sealed class LlmOptions
{
    public string Provider { get; init; } = "Gemini";
    public GeminiOptions Gemini { get; init; } = new();
    public OllamaOptions Ollama { get; init; } = new();
    public MockOptions Mock { get; init; } = new();
}

public sealed class GeminiOptions
{
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com";
    public string ApiKey { get; init; } = string.Empty;
    public string ModelId { get; init; } = "gemini-2.0-flash";
}

public sealed class OllamaOptions
{
    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string ModelId { get; init; } = "llama3.2";
}

public sealed class MockOptions
{
    public int TokenDelayMs { get; init; } = 32;
}
