using Application.Database.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;

namespace WebApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class DatabaseDumpController : ControllerBase
{
    private readonly IDatabaseExportImportService _exportImportService;

    public DatabaseDumpController(IDatabaseExportImportService exportImportService)
    {
        _exportImportService = exportImportService;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        DisableMinResponseDataRate();

        var result = await _exportImportService.Export(cancellationToken);
        if (result.IsFailure)
            return BadRequest(result.Error);

        var dump = result.Value;
        var stream = dump.OpenRead();
        return File(stream, dump.ContentType, dump.FileName);
    }

    [HttpPost("import")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken cancellationToken)
    {
        DisableMaxRequestBodySize();

        if (file == null || file.Length == 0)
            return BadRequest("Не выбран файл архива");

        var result = await _exportImportService.Import(file, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    private void DisableMinResponseDataRate()
    {
        var minRate = HttpContext.Features.Get<IHttpMinResponseDataRateFeature>();
        if (minRate != null)
            minRate.MinDataRate = null;
    }

    private void DisableMaxRequestBodySize()
    {
        var maxSize = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxSize is { IsReadOnly: false })
            maxSize.MaxRequestBodySize = null;
    }
}
