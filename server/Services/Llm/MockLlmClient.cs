using Microsoft.Extensions.Options;
using server.Configuration;

namespace server.Services.Llm;

public sealed class MockLlmClient(IOptionsMonitor<LlmOptions> optionsMonitor) : ILlmClient
{
    public string Name => "Mock";

    public async IAsyncEnumerable<LlmStreamToken> StreamCompletionAsync(
        string systemPrompt,
        IReadOnlyList<LlmTurn> turns,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var options = optionsMonitor.CurrentValue.Mock;
        var latestUserMessage = turns.LastOrDefault(t => t.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content
            ?? "No user message provided.";

        var mockResponse =
            $"Mock mode response. You asked: \"{latestUserMessage}\". "
            + "I would compare KPI trends, identify strongest and weakest regions, then propose two actions to improve churn.";

        foreach (var token in mockResponse.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new LlmStreamToken($"{token} ", Name, "mock-v1");

            if (options.TokenDelayMs > 0)
            {
                await Task.Delay(options.TokenDelayMs, cancellationToken);
            }
        }
    }
}
