using System.Security.Claims;
using api.Dtos.KnowledgeBase;
using api.Infrastructure.Authentication;
using api.Managers.KnowledgeBase.Dtos;
using api.Managers.KnowledgeBase.Interfaces;
using api.Repositories.KnowledgeDocuments.Interfaces;
using api.Repositories.KnowledgeGroups.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace api.Controllers;

// Each standard key has its own private knowledge base. Everything is scoped to the caller's key_id,
// so another key's groups/documents answer 404, never 403.
[ApiController]
[Route("[controller]/[action]")]
[Authorize(Policy = AuthorizationPolicies.StandardKeyOnly)]
public class KnowledgeBaseController(
    IKnowledgeBaseManager knowledgeBaseManager,
    IKnowledgeGroupRepository knowledgeGroupRepository,
    IKnowledgeDocumentRepository knowledgeDocumentRepository) : ControllerBase
{
    private const string MarkdownSourceHeader = "X-Markdown-Source";
    private const string MarkdownLlmProgressHeader = "X-Markdown-Llm-Progress";

    private Guid ApiKeyId => Guid.Parse(User.FindFirstValue(ApiKeyClaimTypes.KeyId)!);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateGroupKnowledgeBaseResponseDto>> CreateGroup(CreateGroupKnowledgeBaseRequestDto request, CancellationToken cancellationToken)
    {
        var group = await knowledgeGroupRepository.CreateAsync(request.ToCreateKnowledgeGroupDto(ApiKeyId), cancellationToken);
        if (group is null)
        {
            return Conflict();
        }

        return Ok(group.ToCreateGroupKnowledgeBaseResponseDto());
    }

    [HttpGet]
    public async Task<ActionResult<List<ListGroupsKnowledgeBaseResponseDto>>> ListGroups(CancellationToken cancellationToken)
    {
        var groups = await knowledgeGroupRepository.GetAllAsync(ApiKeyId, cancellationToken);
        return Ok(groups.Select(group => group.ToListGroupsKnowledgeBaseResponseDto()).ToList());
    }

    // Also deletes the group's documents, chunks, vectors and files.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken cancellationToken) =>
        await knowledgeBaseManager.DeleteGroupAsync(ApiKeyId, id, cancellationToken) ? NoContent() : NotFound();

    // Only stores the file and answers 202 with Status "Processing". A background worker then extracts, chunks,
    // embeds and indexes it; poll GetDocumentText (the Location header) or ListDocuments for Status "Indexed".
    // If that fails the document is still stored (and downloadable) with Status "Failed" and the reason in ErrorMessage.
    [HttpPost]
    [ProducesResponseType<UploadDocumentKnowledgeBaseResponseDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadDocumentKnowledgeBaseResponseDto>> UploadDocument([FromForm] UploadDocumentKnowledgeBaseRequestDto request, CancellationToken cancellationToken)
    {
        var result = await knowledgeBaseManager.UploadDocumentAsync(request.ToUploadDocumentDto(ApiKeyId), cancellationToken);

        return result.Outcome switch
        {
            UploadDocumentOutcome.Uploaded => AcceptedAtAction(
                nameof(GetDocumentText),
                new { id = result.Document!.Id },
                result.Document.ToUploadDocumentKnowledgeBaseResponseDto()),
            UploadDocumentOutcome.GroupNotFound => NotFound(),
            UploadDocumentOutcome.TooLarge => Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                detail: $"File '{request.File.FileName}' exceeds the maximum allowed upload size."),
            UploadDocumentOutcome.UnsupportedFileType => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"File '{request.File.FileName}' has an unsupported file type. Supported types: plain-text files (any extension), " +
                    ".pdf, .docx and images (.png, .jpg, .jpeg, .jfif, .tif, .tiff, .bmp, .gif, .webp, .jp2, .pnm, .pbm, .pgm, .ppm)."),
            _ => throw new InvalidOperationException($"Unknown upload outcome '{result.Outcome}'.")
        };
    }

    [HttpGet]
    public async Task<ActionResult<List<ListDocumentsKnowledgeBaseResponseDto>>> ListDocuments([FromQuery] ListDocumentsKnowledgeBaseRequestDto request, CancellationToken cancellationToken)
    {
        var documents = await knowledgeDocumentRepository.GetAllAsync(ApiKeyId, request.GroupId, cancellationToken);
        return Ok(documents.Select(document => document.ToListDocumentsKnowledgeBaseResponseDto()).ToList());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken) =>
        await knowledgeDocumentRepository.DeleteAsync(ApiKeyId, id, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken cancellationToken)
    {
        var file = await knowledgeDocumentRepository.GetFileAsync(ApiKeyId, id, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        return PhysicalFile(file.AbsolutePath, file.ContentType, file.OriginalFileName);
    }

    // All text extracted from the file. Text is null until Status is "Indexed", so this also shows the processing status.
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetDocumentTextKnowledgeBaseResponseDto>> GetDocumentText(Guid id, CancellationToken cancellationToken)
    {
        var document = await knowledgeDocumentRepository.GetTextAsync(ApiKeyId, id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        return Ok(document.ToGetDocumentTextKnowledgeBaseResponseDto());
    }

    // The document laid out as Markdown (headings, lists, tables), as a text/markdown file (<name>.md) that can be
    // copied or saved as it is. 409 until the document is Indexed. Headers: X-Markdown-Source says how it was made
    // ("Structure", "LlmPending" = an LLM lays it out better shortly, or "Llm"), and X-Markdown-Llm-Progress how far
    // the LLM has got ("12/50") while it works.
    [HttpGet("{id:guid}")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "text/markdown")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExportDocumentMarkdown(Guid id, CancellationToken cancellationToken)
    {
        var document = await knowledgeDocumentRepository.GetMarkdownAsync(ApiKeyId, id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (document.MarkdownText is null)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"The document has no Markdown yet (status {document.Status}).");
        }

        // Inline, so the browser and Swagger UI show the text; the file name is used when it is saved.
        var contentDisposition = new ContentDispositionHeaderValue("inline");
        contentDisposition.SetHttpFileName(document.ToMarkdownFileName());
        Response.Headers.ContentDisposition = contentDisposition.ToString();
        Response.Headers[MarkdownSourceHeader] = document.MarkdownSource;
        if (document.ToLlmProgress() is { } progress)
        {
            Response.Headers[MarkdownLlmProgressHeader] = progress;
        }

        return Content(document.MarkdownText, "text/markdown; charset=utf-8");
    }

    // Searches every group/document the calling key owns, or only one group when GroupId is set.
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SearchKnowledgeBaseResponseDto>>> Search(SearchKnowledgeBaseRequestDto request, CancellationToken cancellationToken)
    {
        var matches = await knowledgeBaseManager.SearchAsync(ApiKeyId, request.Query, request.TopK, request.GroupId, cancellationToken);
        if (matches is null)
        {
            return NotFound();
        }

        return Ok(matches.Select(match => match.ToSearchKnowledgeBaseResponseDto()).ToList());
    }

    // One search on the latest user message, then one answer from the model with the matches as context.
    // Retrieves context from every group the calling key owns, or only one group when GroupId is set.
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RagChatKnowledgeBaseResponseDto>> RagChat(RagChatKnowledgeBaseRequestDto request, CancellationToken cancellationToken)
    {
        var result = await knowledgeBaseManager.RagChatAsync(request.ToRagChatDto(ApiKeyId), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result.ToRagChatKnowledgeBaseResponseDto());
    }

    // A research agent: checks whether the task is clear enough and otherwise asks questions back
    // (Status "NeedsClarification"), else searches the documents in several rounds and answers with citations
    // (Status "Answered"). Stateless like RagChat: send the whole conversation every turn. Takes from a few seconds
    // (questions) to a minute or two (research), depending on the model.
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentChatKnowledgeBaseResponseDto>> AgentChat(AgentChatKnowledgeBaseRequestDto request, CancellationToken cancellationToken)
    {
        var result = await knowledgeBaseManager.AgentChatAsync(request.ToAgentChatDto(ApiKeyId), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result.ToAgentChatKnowledgeBaseResponseDto());
    }
}
