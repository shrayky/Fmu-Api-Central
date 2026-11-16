using Application.Etrn;
using Application.SalePoints;
using Domain.Etrn;
using Domain.Entitys.Organization.Dto;
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
    private readonly IEtrnIncomingLoader _loader;
    private readonly IEtrnJobStatusStore _jobs;
    private readonly SalePointService _salePoints;

    public EtrnController(
        EtrnIncomingQueryService incoming,
        EtrnAcceptanceService acceptance,
        EtrnRequestQueryService requests,
        IEtrnIncomingLoader loader,
        IEtrnJobStatusStore jobs,
        SalePointService salePoints)
    {
        _incoming = incoming;
        _acceptance = acceptance;
        _requests = requests;
        _loader = loader;
        _jobs = jobs;
        _salePoints = salePoints;
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
        [FromQuery] string? number,
        [FromQuery] string? consignor,
        [FromQuery] string? salePoint,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 0)
    {
        var result = await _incoming.ListAsync(
            organizationId ?? "",
            connectionId ?? "",
            status ?? "",
            number ?? "",
            consignor ?? "",
            skip,
            limit,
            salePoint ?? "");
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("incoming/{id}")]
    public async Task<IActionResult> IncomingDocument(string id, CancellationToken cancellationToken)
    {
        await _loader.SyncOpenAsync(id, cancellationToken);
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

    /// <summary>
    /// Сопоставление торговой точки вручную. Пустая строка очищает поле.
    /// </summary>
    [HttpPost("incoming/{id}/sale-point")]
    public async Task<IActionResult> AssignSalePoint(string id, [FromBody] SalePointCommand? command)
    {
        var result = await _salePoints.AssignEtrnAsync(id, command?.SalePointUid ?? "");
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    /// <summary>
    /// Подбор точки по адресу выгрузки: поле заполняется, только если оно пустое.
    /// </summary>
    [HttpPost("incoming/{id}/sale-point/match")]
    public async Task<IActionResult> MatchSalePoint(string id)
    {
        var result = await _salePoints.MatchEtrnAsync(id);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    /// <summary>
    /// Подтверждённое добавление адреса документа в синонимы выбранной точки.
    /// </summary>
    [HttpPost("incoming/{id}/sale-point/alias")]
    public async Task<IActionResult> RememberSalePointAlias(string id)
    {
        var result = await _salePoints.RememberEtrnAliasAsync(id);
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
