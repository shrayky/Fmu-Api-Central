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
    public async Task<IActionResult> Put([FromBody] JsonDocument packet)
    {
        var informationPacket = packet.RootElement.GetRawText();

        var updateResult = await _managerService.UpdateFmuApiInstanceInformation(informationPacket);
        
        return updateResult.IsSuccess ? Ok(updateResult.Value) : BadRequest(updateResult.Error);
    }
}