// Ignore Spelling: App

using Application.Configuration.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace WebApi.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ConfigurationController: ControllerBase
    {
        private readonly IConfigurationApplicationService _configurationService;

        public ConfigurationController(IConfigurationApplicationService parametersService)
        {
            _configurationService = parametersService;
        }

        [HttpGet]
        public async Task<IActionResult> AppConfiguration()
        {
            var jsonConfig = await _configurationService.Current();
            return Content(jsonConfig, "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> AppConfigurationUpload([FromBody] JsonDocument request)
        {
            var content = request.RootElement.GetProperty("Content").GetRawText();
            var success = await _configurationService.Update(content);
            
            if (!success)
                BadRequest("Ошибка обновления конфигурации");

            return Content(content, "application/json");
        }

        [HttpGet("about")]
        [AllowAnonymous]
        public IActionResult About() => Ok(_configurationService.AppInformation());
    }
}