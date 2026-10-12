using api.Dtos.Logs;
using api.Infrastructure.Authentication;
using api.Repositories.Logs.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

// Master key only, since log content can contain sensitive details.
[ApiController]
[Route("[controller]/[action]")]
[Authorize(Policy = AuthorizationPolicies.MasterKeyOnly)]
public class LogsController(ILogRepository logRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SearchLogsResponseDto>> Search([FromQuery] SearchLogsRequestDto request, CancellationToken cancellationToken)
    {
        var page = await logRepository.SearchAsync(request.ToSearchLogsDto(), cancellationToken);
        return Ok(page.ToSearchLogsResponseDto());
    }
}
