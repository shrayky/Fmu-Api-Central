using Domain.Entitys.MarkCheckStatistics.Dto;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class MarkCheckStatisticsController : ControllerBase
{
    private readonly IMarkCheckStatisticsService _statisticsService;

    public MarkCheckStatisticsController(IMarkCheckStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery, Bind(Prefix = "")] MarkCheckStatisticsListFilter? filter = null)
    {
        filter ??= new MarkCheckStatisticsListFilter();

        var result = await _statisticsService.List(page, pageSize, filter);

        return Ok(result);
    }
}
