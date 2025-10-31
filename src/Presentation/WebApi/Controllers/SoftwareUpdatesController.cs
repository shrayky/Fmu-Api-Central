using System.Text.Json;
using Application.SoftwareUpdates.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers.Api;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class SoftwareUpdatesController : ControllerBase
{
    private readonly ILogger<SoftwareUpdatesController> _logger;
    private readonly ISoftwareUpdatesManagerService  _softwareUpdatesManagerService;

    public SoftwareUpdatesController(ILogger<SoftwareUpdatesController> logger,  ISoftwareUpdatesManagerService softwareUpdatesManagerService)
    {
        _logger = logger;
        _softwareUpdatesManagerService = softwareUpdatesManagerService;
    }
    
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _softwareUpdatesManagerService.List(page, pageSize);
        
        if (result.IsSuccess)
            return Ok(result.Value);
        
        return BadRequest(result.Error); 
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download([FromRoute] string id)
    {
        var result = await _softwareUpdatesManagerService.FmuApiUpdateFile(id);

        if (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Add(JsonDocument requestData)
    {
        var data = requestData.RootElement.GetRawText();

        var result = await _softwareUpdatesManagerService.Create(data);
        
        if  (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(result.Error);
    }

    [HttpPost("{id}/attach")]
    public async Task<IActionResult> AttachFile(string id)
    {
        if (Request.Form.Files.Count == 0)
            return BadRequest("Нет файла обновления в запросе");

        var file = Request.Form.Files[0];

        var result = await _softwareUpdatesManagerService.AttachFile(id, file);

        if (result.IsSuccess)
            return Ok(result.Value);

        await _softwareUpdatesManagerService.Delete(id);

        return BadRequest(result.Error);
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _softwareUpdatesManagerService.Delete(id);
        
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}