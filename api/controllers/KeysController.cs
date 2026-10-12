using api.Dtos.Keys;
using api.Infrastructure.Authentication;
using api.Managers.ApiKeys.Interfaces;
using api.Repositories.ApiKeys.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

// Admin endpoints: only the master key may manage keys.
[ApiController]
[Route("[controller]/[action]")]
[Authorize(Policy = AuthorizationPolicies.MasterKeyOnly)]
public class KeysController(IApiKeyRepository apiKeyRepository, IApiKeyManager apiKeyManager) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateKeysResponseDto>> Create(CreateKeysRequestDto request, CancellationToken cancellationToken)
    {
        var rawKey = ApiKeyGenerator.GenerateKey();
        var key = await apiKeyRepository.CreateAsync(
            request.ToCreateApiKeyDto(ApiKeyGenerator.Hash(rawKey), ApiKeyGenerator.Preview(rawKey)),
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = key.Id }, key.ToCreateKeysResponseDto(rawKey));
    }

    [HttpGet]
    public async Task<ActionResult<List<ListKeysResponseDto>>> List(CancellationToken cancellationToken)
    {
        var keys = await apiKeyRepository.GetAllAsync(cancellationToken);
        return Ok(keys.Select(key => key.ToListKeysResponseDto()).ToList());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetByIdKeysResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var key = await apiKeyRepository.GetByIdAsync(id, cancellationToken);
        if (key is null)
        {
            return NotFound();
        }

        return Ok(key.ToGetByIdKeysResponseDto());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdateKeysResponseDto>> Update(Guid id, UpdateKeysRequestDto request, CancellationToken cancellationToken)
    {
        var key = await apiKeyRepository.UpdateAsync(id, request.ToUpdateApiKeyDto(), cancellationToken);
        if (key is null)
        {
            return NotFound();
        }

        return Ok(key.ToUpdateKeysResponseDto());
    }

    // The old key stops working immediately. The new raw key is only shown in this response.
    [HttpPost("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RolloverKeysResponseDto>> Rollover(Guid id, CancellationToken cancellationToken)
    {
        var rawKey = ApiKeyGenerator.GenerateKey();
        var key = await apiKeyRepository.RolloverAsync(id, ApiKeyGenerator.Hash(rawKey), ApiKeyGenerator.Preview(rawKey), cancellationToken);
        if (key is null)
        {
            return NotFound();
        }

        return Ok(key.ToRolloverKeysResponseDto(rawKey));
    }

    [HttpPost("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivateKeysResponseDto>> Activate(Guid id, CancellationToken cancellationToken)
    {
        var key = await apiKeyRepository.SetActiveAsync(id, isActive: true, cancellationToken);
        if (key is null)
        {
            return NotFound();
        }

        return Ok(key.ToActivateKeysResponseDto());
    }

    [HttpPost("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeactivateKeysResponseDto>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var key = await apiKeyRepository.SetActiveAsync(id, isActive: false, cancellationToken);
        if (key is null)
        {
            return NotFound();
        }

        return Ok(key.ToDeactivateKeysResponseDto());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<GetAuditLogKeysResponseDto>>> GetAuditLog(Guid id, CancellationToken cancellationToken)
    {
        var entries = await apiKeyRepository.GetAuditLogAsync(id, cancellationToken);
        if (entries is null)
        {
            return NotFound();
        }

        return Ok(entries.Select(entry => entry.ToGetAuditLogKeysResponseDto()).ToList());
    }

    // Hard delete, including the key's audit log and everything it owns in the knowledge base. Cannot be undone.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await apiKeyManager.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
