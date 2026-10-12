using api.Dtos.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

// Anonymous on purpose, for load balancers and uptime monitors.
[ApiController]
[Route("[controller]/[action]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private const long MaxAllocatedBytes = 1024L * 1024 * 1024;

    [HttpGet]
    [ProducesResponseType<CheckHealthResponseDto>(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<CheckHealthResponseDto> Check(CancellationToken cancellationToken)
    {
        var allocatedBytes = GC.GetTotalMemory(forceFullCollection: false);
        var response = HealthMapper.ToCheckHealthResponseDto(allocatedBytes, MaxAllocatedBytes);
        return allocatedBytes < MaxAllocatedBytes
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
