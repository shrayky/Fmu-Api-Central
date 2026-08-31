using Domain.Entitys.SettingsSchema.Dto;
using Domain.Entitys.SettingsSchema.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class SettingsSchemaController : ControllerBase
{
    private readonly ISettingsSchemaManagerService _manager;

    public SettingsSchemaController(ISettingsSchemaManagerService manager)
    {
        _manager = manager;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await _manager.List(page, pageSize));

    [HttpGet("links/all")]
    public async Task<IActionResult> ListAllLinks()
        => Ok(await _manager.AllLinks());

    [HttpGet("defaults")]
    public IActionResult Defaults()
        => Ok(_manager.Defaults());

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] SettingsSchemaView data)
    {
        var createResult = await _manager.Create(data);
        return createResult.IsSuccess ? Ok() : BadRequest(createResult.Error);
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] SettingsSchemaView data)
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
}
