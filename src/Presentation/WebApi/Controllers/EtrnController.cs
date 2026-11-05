using Application.Etrn;
using Domain.Etrn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class EtrnController : ControllerBase
{
    private readonly EtrnIncomingQueryService _incoming;
    private readonly EtrnAcceptanceService _acceptance;
    private readonly EtrnRequestQueryService _requests;
    private readonly IEtrnJobStatusStore _jobs;

    public EtrnController(
        EtrnIncomingQueryService incoming,
        EtrnAcceptanceService acceptance,
        EtrnRequestQueryService requests,
        IEtrnJobStatusStore jobs)
    {
        _incoming = incoming;
        _acceptance = acceptance;
        _requests = requests;
        _jobs = jobs;
    }

    [HttpGet("jobs")]
    public IActionResult Jobs() => Ok(_jobs.Current());

    [HttpGet("filters")]
    public async Task<IActionResult> Filters() => Ok(await _incoming.FiltersAsync());

    [HttpGet("incoming")]
    public async Task<IActionResult> Incoming(
        [FromQuery] string? organizationId,
        [FromQuery] string? connectionId,
        [FromQuery] string? status,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 0)
    {
        var result = await _incoming.ListAsync(
            organizationId ?? "",
            connectionId ?? "",
            status ?? "",
            skip,
            limit);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("incoming/{id}")]
    public async Task<IActionResult> IncomingDocument(string id)
    {
        var result = await _incoming.GetAsync(id);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("incoming/{id}/accept")]
    public async Task<IActionResult> Accept(
        string id,
        [FromBody] EtrnAcceptCommand? command,
        CancellationToken cancellationToken)
    {
        var result = await _acceptance.AcceptAsync(id, command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> Requests(
        [FromQuery] string? organizationId,
        [FromQuery] string? connectionId,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 0)
    {
        var result = await _requests.ListAsync(organizationId ?? "", connectionId ?? "", skip, limit);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("requests/{id}")]
    public async Task<IActionResult> Request(string id)
    {
        var result = await _requests.GetAsync(id);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
