namespace server.Services.Llm;

public sealed record LlmTurn(string Role, string Content);

public sealed record LlmStreamToken(string? Text, string? Provider = null, string? Model = null);

public interface ILlmClient
{
    string Name { get; }

    IAsyncEnumerable<LlmStreamToken> StreamCompletionAsync(
        string systemPrompt,
        IReadOnlyList<LlmTurn> turns,
        CancellationToken cancellationToken
    );
}
