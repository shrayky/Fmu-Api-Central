using Domain.Entitys.CrptViolations.Dto;
using Domain.Entitys.CrptViolations.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CrptViolationsController : ControllerBase
{
    private readonly ICrptViolationsService _service;
    private readonly ICrptViolationsLoaderService _loader;

    public CrptViolationsController(ICrptViolationsService service, ICrptViolationsLoaderService loader)
    {
        _service = service;
        _loader = loader;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery, Bind(Prefix = "")] CrptViolationsListFilter? filter = null)
    {
        filter ??= new CrptViolationsListFilter();

        var result = await _service.List(page, pageSize, filter);

        return Ok(result);
    }

    /// <summary>
    /// Запускает загрузку отклонений ЧЗ сразу, не дожидаясь воркера.
    /// </summary>
    [HttpPost("load")]
    public async Task<IActionResult> Load([FromQuery] string? inn, CancellationToken cancellationToken)
    {
        var result = await _loader.Load(DateOnly.FromDateTime(DateTime.Today), cancellationToken, inn);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
