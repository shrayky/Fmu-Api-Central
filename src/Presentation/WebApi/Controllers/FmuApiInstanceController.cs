using System.Text.Json;
using Application.Instance.DTO;
using Application.Instance.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class FmuApiInstanceController : ControllerBase
{
    private readonly ILogger<FmuApiInstanceController> _logger;
    private readonly IInstanceManagerService  _managerService;

    public FmuApiInstanceController(ILogger<FmuApiInstanceController> logger, IInstanceManagerService managerService)
    {
        _logger = logger;
        _managerService = managerService;
    }
    
    [HttpPut]
    public async Task<IActionResult> Put([FromBody] JsonDocument packet)
    {
        var informationPacket = packet.RootElement.GetRawText();

        var updateResult = await _managerService.UpdateFmuApiInstanceInformation(informationPacket);
        
        return updateResult.IsSuccess ? Ok(updateResult.Value) : BadRequest(updateResult.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] InstanceMonitoringInformation packet)
    {
        var createResult = await _managerService.CreateNew(packet);
        
        return createResult ? Ok() : BadRequest();
    }

    [HttpDelete("{token}")]
    public async Task<IActionResult> Delete(string token)
    {
        var deleteResult = await _managerService.Delete(token);
        
        return deleteResult ? Ok() : BadRequest();
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _managerService.InstancesList(page, pageSize);

        return Ok(result);
    }
}