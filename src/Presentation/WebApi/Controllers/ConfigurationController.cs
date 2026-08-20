// Ignore Spelling: App

using Application.Configuration.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace WebApi.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ConfigurationController : ControllerBase
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

        [HttpGet("export")]
        public async Task<IActionResult> ExportPortable(CancellationToken cancellationToken)
        {
            var result = await _configurationService.ExportPortable(cancellationToken);
            if (result.IsFailure)
                return BadRequest(result.Error);

            var file = result.Value;
            var bytes = Encoding.UTF8.GetBytes(file.Json);
            return File(bytes, file.ContentType, file.FileName);
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportPortable(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Не выбран файл настроек");

            var result = await _configurationService.ImportPortable(file, cancellationToken);
            return result.IsSuccess ? Ok() : BadRequest(result.Error);
        }

        [HttpGet("about")]
        [AllowAnonymous]
        public IActionResult About() => Ok(_configurationService.AppInformation());
    }
}
