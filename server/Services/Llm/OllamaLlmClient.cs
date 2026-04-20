using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using server.Configuration;

namespace server.Services.Llm;

public sealed class OllamaLlmClient(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<LlmOptions> optionsMonitor
) : ILlmClient
{
    public string Name => "Ollama";

    public async IAsyncEnumerable<LlmStreamToken> StreamCompletionAsync(
        string systemPrompt,
        IReadOnlyList<LlmTurn> turns,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var options = optionsMonitor.CurrentValue.Ollama;
        var client = httpClientFactory.CreateClient(nameof(OllamaLlmClient));
        var endpoint = $"{options.BaseUrl.TrimEnd('/')}/api/chat";

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };
        messages.AddRange(turns.Select(turn => new { role = turn.Role, content = turn.Content }));

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = JsonContent.Create(
            new
            {
                model = options.ModelId,
                stream = true,
                messages
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
            throw new InvalidOperationException($"Ollama error ({(int)response.StatusCode}): {details}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                var value = content.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return new LlmStreamToken(value, Name, options.ModelId);
                }
            }
        }
    }
}
