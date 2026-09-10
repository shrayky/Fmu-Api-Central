using Application.MarkingCalendar.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace WebApi.Controllers;

[Route("api/marking-calendar")]
[ApiController]
[Authorize]
public class MarkingCalendarController(IMarkingCalendarService markingCalendarService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await markingCalendarService.GetSchedule(cancellationToken);
        if (result.IsFailure)
            return BadRequest(result.Error);

        return Content(result.Value, "application/json", Encoding.UTF8);
    }
}
