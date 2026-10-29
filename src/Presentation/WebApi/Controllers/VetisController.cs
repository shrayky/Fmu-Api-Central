using Application.Vetis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class VetisController : ControllerBase
{
    private readonly VetisIncomingQueryService _incoming;
    private readonly VetisApplicationService _applications;

    public VetisController(VetisIncomingQueryService incoming, VetisApplicationService applications)
    {
        _incoming = incoming;
        _applications = applications;
    }

    [HttpGet("filters")]
    public async Task<IActionResult> Filters() => Ok(await _incoming.FiltersAsync());

    [HttpGet("incoming")]
    public async Task<IActionResult> Incoming(
        [FromQuery] string? organizationId,
        [FromQuery] string? enterpriseGuid,
        [FromQuery] string? status,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 0)
    {
        var result = await _incoming.ListAsync(organizationId ?? "", enterpriseGuid ?? "", status ?? "", skip, limit);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("incoming/{id}")]
    public async Task<IActionResult> IncomingDocument(string id)
    {
        var result = await _incoming.GetAsync(id);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("applications")]
    public async Task<IActionResult> Applications(
        [FromQuery] string? organizationId,
        [FromQuery] string? enterpriseGuid,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 0)
    {
        var result = await _applications.ListAsync(organizationId ?? "", enterpriseGuid ?? "", skip, limit);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("applications/{id}")]
    public async Task<IActionResult> Application(string id)
    {
        var result = await _applications.GetAsync(id);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
