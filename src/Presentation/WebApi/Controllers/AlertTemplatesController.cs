using Domain.Entitys.AlertTemplates.Dto;
using Domain.Entitys.AlertTemplates.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class AlertTemplatesController : ControllerBase
{
    private readonly IAlertTemplateManager _manager;
    private readonly IAlertTemplateRunService _runService;

    public AlertTemplatesController(IAlertTemplateManager manager, IAlertTemplateRunService runService)
    {
        _manager = manager;
        _runService = runService;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => Ok(await _manager.List(page, pageSize));

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] AlertTemplateView data)
    {
        var createResult = await _manager.Create(data);
        return createResult.IsSuccess ? Ok() : BadRequest(createResult.Error);
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] AlertTemplateView data)
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

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] AlertTemplateView data)
    {
        var previewResult = await _runService.Preview(data.Script);
        return previewResult.IsSuccess ? Ok(previewResult.Value) : BadRequest(previewResult.Error);
    }
}
