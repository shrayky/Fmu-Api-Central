using Domain.Entitys.Organization.Dto;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationManagerService _manager;

    public OrganizationController(IOrganizationManagerService manager)
    {
        _manager = manager;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await _manager.List(page, pageSize));

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] OrganizationView data)
    {
        var createResult = await _manager.Create(data);
        return createResult.IsSuccess ? Ok() : BadRequest(createResult.Error);
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] OrganizationView data)
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
