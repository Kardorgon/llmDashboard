using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using server.Configuration;

namespace server.Services.Llm;

public sealed class GeminiLlmClient(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<LlmOptions> optionsMonitor,
    ILogger<GeminiLlmClient> logger
) : ILlmClient
{
    public string Name => "Gemini";

    public async IAsyncEnumerable<LlmStreamToken> StreamCompletionAsync(
        string systemPrompt,
        IReadOnlyList<LlmTurn> turns,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var options = optionsMonitor.CurrentValue.Gemini;
        var client = httpClientFactory.CreateClient(nameof(GeminiLlmClient));
        var endpoint =
            $"{options.BaseUrl.TrimEnd('/')}/v1beta/models/{options.ModelId}:streamGenerateContent?alt=sse&key={Uri.EscapeDataString(options.ApiKey)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Content = JsonContent.Create(
            new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = turns.Select(
                    turn => new
                    {
                        role = turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user",
                        parts = new[] { new { text = turn.Content } }
                    }
                )
            }
        );

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Gemini error ({(int)response.StatusCode}): {details}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line["data:".Length..].Trim();
            if (payload.Length == 0 || payload.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var textChunk in ExtractTextParts(payload))
            {
                yield return new LlmStreamToken(textChunk, Name, options.ModelId);
            }
        }

        logger.LogInformation("Gemini stream finished.");
    }

    private static IEnumerable<string> ExtractTextParts(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElement)
                    && textElement.ValueKind == JsonValueKind.String)
                {
                    var value = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return value;
                    }
                }
            }
        }
    }
}
