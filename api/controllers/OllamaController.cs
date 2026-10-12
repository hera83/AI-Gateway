using System.Text.Json;
using api.Dtos.Ollama;
using api.Services.Ollama.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class OllamaController(IOllamaService ollamaService) : ControllerBase
{
    // Same casing as the non-streaming JSON responses.
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    // When request.Stream is true the body is newline-delimited JSON (one response object per line,
    // like Ollama's own streaming format) instead of the single object documented here.
    [HttpPost]
    public async Task<ActionResult<GenerateOllamaResponseDto>> Generate(GenerateOllamaRequestDto request, CancellationToken cancellationToken)
    {
        if (!request.Stream)
        {
            var response = await ollamaService.GenerateAsync(request.ToGenerateRequestDto(), cancellationToken);
            return Ok(response.ToGenerateOllamaResponseDto());
        }

        Response.ContentType = "application/x-ndjson";
        await foreach (var chunk in ollamaService.GenerateStreamAsync(request.ToGenerateRequestDto(), cancellationToken))
        {
            await WriteNdjsonLineAsync(chunk.ToGenerateOllamaResponseDto(), cancellationToken);
        }

        return new EmptyResult();
    }

    // Same streaming behavior as Generate.
    [HttpPost]
    public async Task<ActionResult<ChatOllamaResponseDto>> Chat(ChatOllamaRequestDto request, CancellationToken cancellationToken)
    {
        if (!request.Stream)
        {
            var response = await ollamaService.ChatAsync(request.ToChatRequestDto(), cancellationToken);
            return Ok(response.ToChatOllamaResponseDto());
        }

        Response.ContentType = "application/x-ndjson";
        await foreach (var chunk in ollamaService.ChatStreamAsync(request.ToChatRequestDto(), cancellationToken))
        {
            await WriteNdjsonLineAsync(chunk.ToChatOllamaResponseDto(), cancellationToken);
        }

        return new EmptyResult();
    }

    [HttpPost]
    public async Task<ActionResult<EmbedOllamaResponseDto>> Embed(EmbedOllamaRequestDto request, CancellationToken cancellationToken)
    {
        var response = await ollamaService.EmbedAsync(request.ToEmbedRequestDto(), cancellationToken);
        return Ok(response.ToEmbedOllamaResponseDto());
    }

    [HttpGet]
    public async Task<ActionResult<ListModelsOllamaResponseDto>> ListModels(CancellationToken cancellationToken)
    {
        var response = await ollamaService.ListModelsAsync(cancellationToken);
        return Ok(response.ToListModelsOllamaResponseDto());
    }

    [HttpGet]
    public async Task<ActionResult<ListRunningModelsOllamaResponseDto>> ListRunningModels(CancellationToken cancellationToken)
    {
        var response = await ollamaService.ListRunningModelsAsync(cancellationToken);
        return Ok(response.ToListRunningModelsOllamaResponseDto());
    }

    [HttpPost]
    public async Task<ActionResult<ShowModelOllamaResponseDto>> ShowModel(ShowModelOllamaRequestDto request, CancellationToken cancellationToken)
    {
        var response = await ollamaService.ShowModelAsync(request.ToShowModelRequestDto(), cancellationToken);
        return Ok(response.ToShowModelOllamaResponseDto());
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CopyModel(CopyModelOllamaRequestDto request, CancellationToken cancellationToken)
    {
        await ollamaService.CopyModelAsync(request.ToCopyModelRequestDto(), cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteModel([FromQuery] DeleteModelOllamaRequestDto request, CancellationToken cancellationToken)
    {
        await ollamaService.DeleteModelAsync(request.ToDeleteModelRequestDto(), cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<PullModelOllamaResponseDto>> PullModel(PullModelOllamaRequestDto request, CancellationToken cancellationToken)
    {
        var response = await ollamaService.PullModelAsync(request.ToPullModelRequestDto(), cancellationToken);
        return Ok(response.ToPullModelOllamaResponseDto());
    }

    [HttpPost]
    public async Task<ActionResult<PushModelOllamaResponseDto>> PushModel(PushModelOllamaRequestDto request, CancellationToken cancellationToken)
    {
        var response = await ollamaService.PushModelAsync(request.ToPushModelRequestDto(), cancellationToken);
        return Ok(response.ToPushModelOllamaResponseDto());
    }

    [HttpPost]
    public async Task<ActionResult<CreateModelOllamaResponseDto>> CreateModel(CreateModelOllamaRequestDto request, CancellationToken cancellationToken)
    {
        var response = await ollamaService.CreateModelAsync(request.ToCreateModelRequestDto(), cancellationToken);
        return Ok(response.ToCreateModelOllamaResponseDto());
    }

    [HttpGet]
    public async Task<ActionResult<GetVersionOllamaResponseDto>> GetVersion(CancellationToken cancellationToken)
    {
        var response = await ollamaService.GetVersionAsync(cancellationToken);
        return Ok(response.ToGetVersionOllamaResponseDto());
    }

    private async Task WriteNdjsonLineAsync<T>(T value, CancellationToken cancellationToken)
    {
        await Response.WriteAsync(JsonSerializer.Serialize(value, StreamJsonOptions) + "\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
