using System.Text.Json;
using Application.Instance.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
public class FmuApiInstanceMonitoringController : ControllerBase
{
    private readonly ILogger<FmuApiInstanceMonitoringController> _logger;
    private readonly IInstanceManagerService  _managerService;

    public FmuApiInstanceMonitoringController(ILogger<FmuApiInstanceMonitoringController> logger, IInstanceManagerService managerService)
    {
        _logger = logger;
        _managerService = managerService;
    }
    
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] JsonDocument packet)
    {
        var informationPacket = packet.RootElement.GetRawText();

        var updateResult = await _managerService.UpdateFmuApiInstanceInformation(informationPacket);
        
        return updateResult.IsSuccess ? Ok(updateResult.Value) : BadRequest(updateResult.Error);
    }

    [HttpGet("settings/{token}")]
    public async Task<IActionResult> SoftwareSettings(string token)
    {
        var settings = await _managerService.InstanceSettings(token);
        
        return Ok(settings);
    }

    [HttpPut("settings/updated/{token}")]
    public async Task<IActionResult> SoftwareSettings(string token, [FromBody] JsonDocument packet)
    {
        var updateResult = await _managerService.SettingsUploaded(token);

        if (updateResult.IsSuccess)
            return Ok();
        
        return BadRequest(updateResult.Error);
    }

    [HttpGet("downloadFmuApiUpdate/{token}")]
    public async Task<IActionResult> DownloadFmuApiUpdate(string token)
    {
        var updateData = await _managerService.FmuApiUpdate(token);
        
        const string fileName = "update.zip";
        const string contentType = "application/octet-stream";
        
        return updateData.IsSuccess ? File(updateData.Value, contentType, fileName) : BadRequest(updateData.Error);
    }
}