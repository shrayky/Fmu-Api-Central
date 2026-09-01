using Domain.Entitys.Organization.Dto;
using Domain.Entitys.Organization.Interfaces;
using Domain.GisMt.Enum;
using Domain.GisMt.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationManagerService _manager;
    private readonly IGisMtExchangeService _gisMtExchange;

    public OrganizationController(IOrganizationManagerService manager, IGisMtExchangeService gisMtExchange)
    {
        _manager = manager;
        _gisMtExchange = gisMtExchange;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await _manager.List(page, pageSize));

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] OrganizationView data)
    {
        var createResult = await _manager.Create(data);
        return createResult.IsSuccess ? Ok() : BadRequest(createResult.Error);
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] OrganizationView data)
    {
        var updateResult = await _manager.Update(data);
        return updateResult.IsSuccess ? Ok() : BadRequest(updateResult.Error);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleteResult = await _manager.Delete(id);
        return deleteResult.IsSuccess ? Ok() : BadRequest(deleteResult.Error);
    }

    /// <summary>
    /// Ставит в очередь получение товарных групп ГИС МТ.
    /// </summary>
    [HttpPost("{id}/gismt/product-groups")]
    public Task<IActionResult> ProductGroups(string id, CancellationToken cancellationToken)
        => Enqueue(id, GisMtManualOperationKind.ProductGroups, cancellationToken);

    /// <summary>
    /// Ставит в очередь загрузку документов ГИС МТ.
    /// </summary>
    [HttpPost("{id}/gismt/documents")]
    public Task<IActionResult> Documents(string id, CancellationToken cancellationToken)
        => Enqueue(id, GisMtManualOperationKind.Documents, cancellationToken);

    /// <summary>
    /// Ставит в очередь загрузку остатков ГИС МТ.
    /// </summary>
    [HttpPost("{id}/gismt/stock")]
    public Task<IActionResult> Stock(string id, CancellationToken cancellationToken)
        => Enqueue(id, GisMtManualOperationKind.Stock, cancellationToken);

    /// <summary>
    /// Проксирует операцию на GisMt и возвращает Ok или ошибку.
    /// </summary>
    private async Task<IActionResult> Enqueue(
        string id,
        GisMtManualOperationKind kind,
        CancellationToken cancellationToken)
    {
        var result = await _gisMtExchange.ManualOperation(id, kind, cancellationToken);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
