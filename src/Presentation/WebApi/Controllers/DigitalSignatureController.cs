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
}
