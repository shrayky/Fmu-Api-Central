using Application.Logs.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LogsController : ControllerBase
    {
        private readonly ILogInfoService _logInfoService;

        public LogsController(ILogInfoService logInfoService)
        {
            _logInfoService = logInfoService;
        }

        [HttpGet("{logFileName}")]
        public async Task<IActionResult> LogsPacket(string logFileName)
        {
            return Ok(await _logInfoService.Packet(logFileName));
        }
    }
}
