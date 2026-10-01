using Domain.TrueApiIntegration.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class CryptoProLicenseController : ControllerBase
{
    private readonly ICryptoProLicenseService _service;

    public CryptoProLicenseController(ICryptoProLicenseService service)
    {
        _service = service;
    }

    [HttpGet]
    public IActionResult View()
    {
        var result = _service.View();
        return result.IsSuccess ? Ok(new { text = result.Value }) : PlainError(result.Error);
    }

    [HttpPost]
    public IActionResult Set([FromBody] CryptoProLicenseRequest request)
    {
        var result = _service.Set(request?.Serial ?? string.Empty);
        return result.IsSuccess ? Ok(new { text = result.Value }) : PlainError(result.Error);
    }

    private ContentResult PlainError(string error) => new()
    {
        StatusCode = StatusCodes.Status400BadRequest,
        Content = error,
        ContentType = "text/plain; charset=utf-8"
    };

    public sealed class CryptoProLicenseRequest
    {
        public string Serial { get; set; } = string.Empty;
    }
}
