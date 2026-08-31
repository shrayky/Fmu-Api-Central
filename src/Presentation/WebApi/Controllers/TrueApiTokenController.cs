using Domain.Entitys.Organization.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("/api/ts/token")]
[Authorize]
public class TrueApiTokenController : ControllerBase
{
    private readonly IOrganizationManagerService _manager;

    public TrueApiTokenController(IOrganizationManagerService manager)
    {
        _manager = manager;
    }

    [HttpGet("inn")]
    public async Task<IActionResult> Token([FromQuery] string inn)
    {
        var result = await _manager.Token(inn);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
