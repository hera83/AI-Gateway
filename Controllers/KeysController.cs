using AiGateway.Authentication;
using AiGateway.Dto.Errors;
using AiGateway.Dto.Keys;
using AiGateway.Service.Keys.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SvcDto = AiGateway.Service.Keys.Dtos;

namespace AiGateway.Controllers;

[ApiController]
[Route("[controller]/[action]")]
[Authorize(Roles = ApiKeyAuthenticationHandler.AdministratorRole)]
[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
public class KeysController(IKeyService keyService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateKeyResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateKeyRequestDto request, CancellationToken cancellationToken)
    {
        var result = await keyService.CreateAsync(ToService(request), cancellationToken);

        var response = new CreateKeyResponseDto
        {
            Id = result.Id,
            Name = result.Name,
            ResponsibleName = result.ResponsibleName,
            ContactInfo = result.ContactInfo,
            IsActive = result.IsActive,
            ExpiresAt = result.ExpiresAt,
            CreatedAt = result.CreatedAt,
            LastRotatedAt = result.LastRotatedAt,
            LastUsedAt = result.LastUsedAt,
            ApiKey = result.ApiKey
        };

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListKeysResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await keyService.ListAsync(cancellationToken);
        return Ok(new ListKeysResponseDto { Keys = result.Select(ToApi).ToList() });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(KeyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await keyService.GetByIdAsync(id, cancellationToken);
        return Ok(ToApi(result));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(KeyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKeyRequestDto request, CancellationToken cancellationToken)
    {
        var result = await keyService.UpdateAsync(id, ToService(request), cancellationToken);
        return Ok(ToApi(result));
    }

    [HttpPost("{id:guid}")]
    [ProducesResponseType(typeof(RolloverKeyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rollover(Guid id, CancellationToken cancellationToken)
    {
        var result = await keyService.RolloverAsync(id, cancellationToken);
        return Ok(new RolloverKeyResponseDto
        {
            Id = result.Id,
            ApiKey = result.ApiKey,
            LastRotatedAt = result.LastRotatedAt
        });
    }

    [HttpPost("{id:guid}")]
    [ProducesResponseType(typeof(KeyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await keyService.SetActiveAsync(id, isActive: true, cancellationToken);
        return Ok(ToApi(result));
    }

    [HttpPost("{id:guid}")]
    [ProducesResponseType(typeof(KeyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await keyService.SetActiveAsync(id, isActive: false, cancellationToken);
        return Ok(ToApi(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditLogResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLog(Guid id, CancellationToken cancellationToken)
    {
        var result = await keyService.GetAuditLogAsync(id, cancellationToken);
        return Ok(new AuditLogResponseDto
        {
            Entries = result
                .Select(entry => new AuditLogEntryResponseDto
                {
                    Action = entry.Action,
                    Timestamp = entry.Timestamp,
                    Details = entry.Details
                })
                .ToList()
        });
    }

    private static SvcDto.CreateKeyDto ToService(CreateKeyRequestDto request) => new()
    {
        Name = request.Name,
        ResponsibleName = request.ResponsibleName,
        ContactInfo = request.ContactInfo,
        ExpiresAt = request.ExpiresAt
    };

    private static SvcDto.UpdateKeyDto ToService(UpdateKeyRequestDto request) => new()
    {
        Name = request.Name,
        ResponsibleName = request.ResponsibleName,
        ContactInfo = request.ContactInfo,
        ExpiresAt = request.ExpiresAt
    };

    private static KeyResponseDto ToApi(SvcDto.KeyDto key) => new()
    {
        Id = key.Id,
        Name = key.Name,
        ResponsibleName = key.ResponsibleName,
        ContactInfo = key.ContactInfo,
        IsActive = key.IsActive,
        ExpiresAt = key.ExpiresAt,
        CreatedAt = key.CreatedAt,
        LastRotatedAt = key.LastRotatedAt,
        LastUsedAt = key.LastUsedAt
    };
}
