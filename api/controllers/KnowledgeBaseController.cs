using System.Security.Claims;
using api.Dtos.KnowledgeBase;
using api.Infrastructure.Authentication;
using api.Managers.KnowledgeBase.Dtos;
using api.Managers.KnowledgeBase.Interfaces;
using api.Repositories.KnowledgeDocuments.Interfaces;
using api.Repositories.KnowledgeGroups.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    // Extracted, chunked, embedded and indexed synchronously. If extraction, embedding or indexing fails the
    // document is still stored (and downloadable) with Status "Failed" and the reason in ErrorMessage.
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<UploadDocumentKnowledgeBaseResponseDto>> UploadDocument([FromForm] UploadDocumentKnowledgeBaseRequestDto request, CancellationToken cancellationToken)
    {
        var result = await knowledgeBaseManager.UploadDocumentAsync(request.ToUploadDocumentDto(ApiKeyId), cancellationToken);

        return result.Outcome switch
        {
            UploadDocumentOutcome.Uploaded => Ok(result.Document!.ToUploadDocumentKnowledgeBaseResponseDto()),
            UploadDocumentOutcome.GroupNotFound => NotFound(),
            UploadDocumentOutcome.TooLarge => Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                detail: $"File '{request.File.FileName}' exceeds the maximum allowed upload size."),
            UploadDocumentOutcome.UnsupportedFileType => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"File '{request.File.FileName}' has an unsupported file type. Supported types: .txt, .md, .pdf, .docx."),
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

    // Searches across every group/document the calling key owns.
    [HttpPost]
    public async Task<ActionResult<List<SearchKnowledgeBaseResponseDto>>> Search(SearchKnowledgeBaseRequestDto request, CancellationToken cancellationToken)
    {
        var matches = await knowledgeBaseManager.SearchAsync(ApiKeyId, request.Query, request.TopK, cancellationToken);
        return Ok(matches.Select(match => match.ToSearchKnowledgeBaseResponseDto()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<RagChatKnowledgeBaseResponseDto>> RagChat(RagChatKnowledgeBaseRequestDto request, CancellationToken cancellationToken)
    {
        var result = await knowledgeBaseManager.RagChatAsync(request.ToRagChatDto(ApiKeyId), cancellationToken);
        return Ok(result.ToRagChatKnowledgeBaseResponseDto());
    }
}
