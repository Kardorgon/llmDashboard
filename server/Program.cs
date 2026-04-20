using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using server.Configuration;
using server.Contracts;
using server.Services;
using server.Services.Llm;
using server.Services.Rag;

var sseJsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("Llm"));
builder.Services.AddOpenApi();
builder.Services.AddHttpClient(nameof(GeminiLlmClient));
builder.Services.AddHttpClient(nameof(OllamaLlmClient));

builder.Services.AddSingleton<GeminiLlmClient>();
builder.Services.AddSingleton<OllamaLlmClient>();
builder.Services.AddSingleton<MockLlmClient>();
builder.Services.AddSingleton<LlmClientRouter>();
builder.Services.AddSingleton<RagDocumentRepository>();
builder.Services.AddSingleton<RagService>();
builder.Services.AddSingleton<ChatOrchestrator>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "ClientOrigin",
        corsPolicyBuilder =>
        {
            corsPolicyBuilder
                .WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("ClientOrigin");

app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }));

app.MapPost(
        "/api/chat/stream",
        async (
            HttpContext context,
            [FromBody] ChatRequestDto request,
            ChatOrchestrator orchestrator,
            CancellationToken cancellationToken
        ) =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";

            await using var writer = new StreamWriter(context.Response.Body);
            try
            {
                await foreach (var chunk in orchestrator.StreamAnswerAsync(request, cancellationToken))
                {
                    await writer.WriteAsync($"data: {JsonSerializer.Serialize(chunk, sseJsonOptions)}\n\n");
                    await writer.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected; no-op.
            }
            catch (Exception exception)
            {
                var errorChunk = new ChatStreamChunkDto("error", Message: exception.Message);
                await writer.WriteAsync($"data: {JsonSerializer.Serialize(errorChunk, sseJsonOptions)}\n\n");
                await writer.FlushAsync(cancellationToken);
            }
        }
    )
    .WithName("StreamChat")
    .WithOpenApi();

app.Run();
