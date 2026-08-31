using Domain.Entitys.InstanceGroup.Dto;
using Domain.Entitys.InstanceGroup.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class InstanceGroupController : ControllerBase
{
    private readonly IInstanceGroupManagerService _manager;

    public InstanceGroupController(IInstanceGroupManagerService manager)
    {
        _manager = manager;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await _manager.List(page, pageSize));

    [HttpGet("links/all")]
    public async Task<IActionResult> ListAllLinks()
        => Ok(await _manager.AllLinks());

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] InstanceGroupView data)
    {
        var createResult = await _manager.Create(data);
        return createResult.IsSuccess ? Ok() : BadRequest(createResult.Error);
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] InstanceGroupView data)
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

    [HttpPost("force-update")]
    public async Task<IActionResult> ForceUpdate([FromBody] GroupForceUpdateRequest request)
    {
        var result = await _manager.AssignForcedUpdate(request.GroupIds, request.UpdateId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
