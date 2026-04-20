using System.Text;

namespace server.Services.Rag;

public sealed class RagDocumentRepository(IWebHostEnvironment environment, ILogger<RagDocumentRepository> logger)
{
    private IReadOnlyList<RagChunk>? _cachedChunks;

    public IReadOnlyList<RagChunk> GetChunks()
    {
        if (_cachedChunks is not null)
        {
            return _cachedChunks;
        }

        _cachedChunks = LoadChunks();
        logger.LogInformation("Loaded {ChunkCount} RAG chunks.", _cachedChunks.Count);
        return _cachedChunks;
    }

    private IReadOnlyList<RagChunk> LoadChunks()
    {
        var documentsPath = Path.Combine(environment.ContentRootPath, "Rag", "Documents");
        if (!Directory.Exists(documentsPath))
        {
            logger.LogWarning("RAG documents path does not exist: {Path}", documentsPath);
            return [];
        }

        var chunks = new List<RagChunk>();
        var files = Directory
            .EnumerateFiles(documentsPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path);

        foreach (var file in files)
        {
            var documentName = Path.GetFileName(file);
            var content = File.ReadAllText(file, Encoding.UTF8);
            var paragraphs = content
                .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var idx = 0;
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Length < 25)
                {
                    continue;
                }

                chunks.Add(new RagChunk(documentName, paragraph, idx));
                idx += 1;
            }
        }

        return chunks;
    }
}
