using Application.Vetis;
using Domain.Vetis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class VetisController : ControllerBase
{
    private readonly VetisIncomingQueryService _incoming;
    private readonly VetisIncomingQuenchService _quench;
    private readonly VetisQuenchProgressStore _progress;
    private readonly VetisApplicationService _applications;

    public VetisController(
        VetisIncomingQueryService incoming,
        VetisIncomingQuenchService quench,
        VetisQuenchProgressStore progress,
        VetisApplicationService applications)
    {
        _incoming = incoming;
        _quench = quench;
        _progress = progress;
        _applications = applications;
    }

    [HttpGet("filters")]
    public async Task<IActionResult> Filters() => Ok(await _incoming.FiltersAsync());

    [HttpGet("not-accepted-counts")]
    public async Task<IActionResult> NotAcceptedCounts()
    {
        var result = await _incoming.NotAcceptedCountsAsync();
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

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

    [HttpGet("incoming/{id}/quench")]
    public IActionResult QuenchProgress(string id) =>
        Ok(_progress.Find(id) ?? new VetisQuenchProgress());

    [HttpPost("incoming/{id}/quench")]
    public async Task<IActionResult> Quench(string id, [FromBody] VetisQuenchCommand? command, CancellationToken cancellationToken)
    {
        var result = await _quench.QuenchAsync(id, command?.Facts ?? [], cancellationToken);
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
