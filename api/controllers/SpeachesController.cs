using api.Dtos.Speaches;
using api.Services.Speaches.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class SpeachesController(ISpeachesService speachesService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatCompletionsSpeachesResponseDto>> ChatCompletions(ChatCompletionsSpeachesRequestDto request, CancellationToken cancellationToken)
    {
        var response = await speachesService.ChatCompletionsAsync(request.ToChatCompletionRequestDto(), cancellationToken);
        return Ok(response.ToChatCompletionsSpeachesResponseDto());
    }

    [HttpPost]
    public async Task<ActionResult<TranscribeSpeachesResponseDto>> Transcribe([FromForm] TranscribeSpeachesRequestDto request, CancellationToken cancellationToken)
    {
        var response = await speachesService.TranscribeAsync(request.ToTranscriptionRequestDto(), cancellationToken);
        return Ok(response.ToTranscribeSpeachesResponseDto());
    }

    // WebSocket upgrade, not request/response: the caller opens ws(s)://.../Speaches/TranscribeRealtime?Model=...,
    // streams audio in and receives Speaches' realtime transcription events back on the same connection.
    // Errors (400, or 502 when Speaches can't be reached) are only possible before the upgrade.
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status101SwitchingProtocols)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> TranscribeRealtime([FromQuery] TranscribeRealtimeSpeachesRequestDto request, CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "This endpoint only accepts WebSocket upgrade requests.");
        }

        await speachesService.ProxyRealtimeTranscriptionAsync(
            () => HttpContext.WebSockets.AcceptWebSocketAsync(), request.Model, request.Language, cancellationToken);
        return new EmptyResult();
    }

    [HttpPost]
    public async Task<ActionResult<TranslateSpeachesResponseDto>> Translate([FromForm] TranslateSpeachesRequestDto request, CancellationToken cancellationToken)
    {
        var response = await speachesService.TranslateAsync(request.ToTranslationRequestDto(), cancellationToken);
        return Ok(response.ToTranslateSpeachesResponseDto());
    }

    [HttpGet]
    public async Task<ActionResult<ListModelsSpeachesResponseDto>> ListModels([FromQuery] ListModelsSpeachesRequestDto request, CancellationToken cancellationToken)
    {
        var response = await speachesService.ListModelsAsync(request.Task, cancellationToken);
        return Ok(response.ToListModelsSpeachesResponseDto());
    }

    [HttpGet]
    public async Task<ActionResult<ListAudioModelsSpeachesResponseDto>> ListAudioModels(CancellationToken cancellationToken)
    {
        var response = await speachesService.ListAudioModelsAsync(cancellationToken);
        return Ok(response.ToListAudioModelsSpeachesResponseDto());
    }

    [HttpGet]
    public async Task<ActionResult<ListVoicesSpeachesResponseDto>> ListVoices(CancellationToken cancellationToken)
    {
        var response = await speachesService.ListVoicesAsync(cancellationToken);
        return Ok(response.ToListVoicesSpeachesResponseDto());
    }

    [HttpGet("{modelId}")]
    public async Task<ActionResult<GetModelSpeachesResponseDto>> GetModel(string modelId, CancellationToken cancellationToken)
    {
        var response = await speachesService.GetModelAsync(modelId, cancellationToken);
        return Ok(response.ToGetModelSpeachesResponseDto());
    }

    [HttpPost("{modelId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DownloadModel(string modelId, CancellationToken cancellationToken)
    {
        await speachesService.DownloadModelAsync(modelId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{modelId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteModel(string modelId, CancellationToken cancellationToken)
    {
        await speachesService.DeleteModelAsync(modelId, cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<GetRegistrySpeachesResponseDto>> GetRegistry([FromQuery] GetRegistrySpeachesRequestDto request, CancellationToken cancellationToken)
    {
        var response = await speachesService.GetRegistryAsync(request.Task, cancellationToken);
        return Ok(response.ToGetRegistrySpeachesResponseDto());
    }

    [HttpGet]
    public async Task<ActionResult<ListRunningModelsSpeachesResponseDto>> ListRunningModels(CancellationToken cancellationToken)
    {
        var response = await speachesService.ListRunningModelsAsync(cancellationToken);
        return Ok(response.ToListRunningModelsSpeachesResponseDto());
    }

    [HttpPost("{modelId}")]
    public async Task<ActionResult<LoadModelSpeachesResponseDto>> LoadModel(string modelId, CancellationToken cancellationToken)
    {
        var response = await speachesService.LoadModelAsync(modelId, cancellationToken);
        return Ok(response.ToLoadModelSpeachesResponseDto());
    }

    [HttpDelete("{modelId}")]
    public async Task<ActionResult<StopModelSpeachesResponseDto>> StopModel(string modelId, CancellationToken cancellationToken)
    {
        var response = await speachesService.StopModelAsync(modelId, cancellationToken);
        return Ok(response.ToStopModelSpeachesResponseDto());
    }

    // A download filename adds Content-Disposition: attachment, so Swagger UI and browsers offer the audio as
    // a download instead of (unreliably) previewing it. Programmatic callers ignore the header.
    [HttpPost]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "audio/mpeg")]
    public async Task<IActionResult> SynthesizeSpeech(SynthesizeSpeechSpeachesRequestDto request, CancellationToken cancellationToken)
    {
        var audio = await speachesService.SynthesizeSpeechAsync(request.ToSynthesizeRequestDto(), cancellationToken);
        return File(audio.Content, audio.ContentType, $"speech.{request.ResponseFormat}");
    }

    [HttpPost]
    public async Task<ActionResult<CreateSpeechEmbeddingSpeachesResponseDto>> CreateSpeechEmbedding([FromForm] CreateSpeechEmbeddingSpeachesRequestDto request, CancellationToken cancellationToken)
    {
        var response = await speachesService.CreateSpeechEmbeddingAsync(request.ToSpeechEmbeddingRequestDto(), cancellationToken);
        return Ok(response.ToCreateSpeechEmbeddingSpeachesResponseDto());
    }

    [HttpPost]
    public async Task<ActionResult<List<DetectSpeechTimestampsSpeachesResponseDto>>> DetectSpeechTimestamps([FromForm] DetectSpeechTimestampsSpeachesRequestDto request, CancellationToken cancellationToken)
    {
        var timestamps = await speachesService.DetectSpeechTimestampsAsync(request.ToSpeechTimestampsRequestDto(), cancellationToken);
        return Ok(timestamps.Select(timestamp => timestamp.ToDetectSpeechTimestampsSpeachesResponseDto()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<DiarizeSpeachesResponseDto>> Diarize([FromForm] DiarizeSpeachesRequestDto request, CancellationToken cancellationToken)
    {
        var response = await speachesService.DiarizeAsync(request.ToDiarizationRequestDto(), cancellationToken);
        return Ok(response.ToDiarizeSpeachesResponseDto());
    }
}
