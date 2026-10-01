using Domain.TrueApiIntegration.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class DigitalSignatureController : ControllerBase
{
    private readonly IDigitalSignatureService _service;

    public DigitalSignatureController(IDigitalSignatureService service)
    {
        _service = service;
    }

    [HttpGet]
    public IActionResult List() => Ok(_service.List());

    [HttpPost]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public IActionResult Install(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Не выбран файл архива");

        if (file.Length > 2 * 1024 * 1024)
            return BadRequest("Архив сертификата больше 2 МБ");

        using var stream = file.OpenReadStream();
        var result = _service.Install(stream);
        if (result.IsFailure)
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Content = result.Error,
                ContentType = "text/plain; charset=utf-8"
            };
        }

        return Ok(_service.List());
    }
}
