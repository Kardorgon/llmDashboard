namespace server.Services.Rag;

public sealed record RagChunk(string DocumentName, string Text, int Index);

public sealed record RagContextResult(string Context, IReadOnlyList<RagChunk> Chunks);
