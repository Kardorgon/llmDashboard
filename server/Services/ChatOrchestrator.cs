using server.Contracts;
using server.Services.Llm;
using server.Services.Rag;

namespace server.Services;

public sealed class ChatOrchestrator(LlmClientRouter clientRouter, RagService ragService)
{
    public async IAsyncEnumerable<ChatStreamChunkDto> StreamAnswerAsync(
        ChatRequestDto request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var sanitizedTurns = request.Messages
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new LlmTurn(message.Role, message.Content.Trim()))
            .ToList();

        var userPrompt = sanitizedTurns.LastOrDefault(turn => turn.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content
            ?? "No user prompt provided.";

        var ragContext = ragService.BuildContext(userPrompt, request.DashboardSnapshot);
        var systemPrompt = BuildSystemPrompt(ragContext.Context);
        var llmClient = clientRouter.Resolve();

        yield return new ChatStreamChunkDto("meta", Provider: llmClient.Name);

        await foreach (var token in llmClient.StreamCompletionAsync(systemPrompt, sanitizedTurns, cancellationToken))
        {
            if (!string.IsNullOrEmpty(token.Text))
            {
                yield return new ChatStreamChunkDto("chunk", Delta: token.Text, Provider: token.Provider, Model: token.Model);
            }
        }

        yield return new ChatStreamChunkDto("done", Provider: llmClient.Name);
    }

    private static string BuildSystemPrompt(string ragContext)
    {
        return
            """
            You are a dashboard copilot for a SaaS sales team.
            Use only the CONTEXT section and the dashboard values supplied by the caller.
            If information is missing, explicitly say what is missing instead of inventing.
            Keep answers concise, practical, and data-focused.

            CONTEXT:
            """
            + Environment.NewLine
            + ragContext;
    }
}
