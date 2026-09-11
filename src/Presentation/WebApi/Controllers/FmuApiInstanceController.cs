using System.Text.Json;
using Application.Instance.Interfaces;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Instance.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class FmuApiInstanceController : ControllerBase
{
    private readonly IInstanceManagerService  _managerService;
    private readonly ICheckerDistributionService _checkerDistribution;

    public FmuApiInstanceController(
        IInstanceManagerService managerService,
        ICheckerDistributionService checkerDistribution)
    {
        _managerService = managerService;
        _checkerDistribution = checkerDistribution;
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
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery, Bind(Prefix = "")] InstanceListFilter? filter = null)
    {
        if (filter == null)
            filter = new InstanceListFilter();

        var result = await _managerService.InstancesList(page, pageSize, filter);

        return Ok(result);
    }

    [HttpPost("force-update")]
    public async Task<IActionResult> ForceUpdate([FromBody] ForceUpdateRequest request)
    {
        var result = await _managerService.AssignForcedUpdate(request.Tokens, request.UpdateId);

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{token}/checker-distribution")]
    public async Task<IActionResult> DownloadCheckerDistribution(string token)
    {
        var result = await _checkerDistribution.Build(token);

        if (result.IsFailure)
            return BadRequest(result.Error);

        var download = result.Value;
        return new FileStreamResult(download.Content, download.ContentType)
        {
            FileDownloadName = download.FileName
        };
    }
}