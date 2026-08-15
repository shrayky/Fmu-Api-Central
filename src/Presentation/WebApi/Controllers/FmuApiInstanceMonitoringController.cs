using System.Text.Json;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.SoftwareUpdateFiles;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FmuApiInstanceMonitoringController : ControllerBase
{
    private readonly IInstanceManagerService  _managerService;

    public FmuApiInstanceMonitoringController(IInstanceManagerService managerService)
    {
        _managerService = managerService;
    }
    
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] JsonDocument packet)
    {
        var informationPacket = packet.RootElement.GetRawText();

        var updateResult = await _managerService.UpdateFmuApiInstanceInformation(informationPacket);
        
        return updateResult.IsSuccess ? Ok(updateResult.Value) : BadRequest(updateResult.Error);
    }

    [HttpGet("settings/{token}")]
    public async Task<IActionResult> SoftwareSettings(string token)
    {
        var settings = await _managerService.InstanceSettings(token);
        
        return Ok(settings);
    }

    [HttpPut("settings/updated/{token}")]
    public async Task<IActionResult> SoftwareSettings(string token, [FromBody] JsonDocument packet)
    {
        var updateResult = await _managerService.SettingsUploaded(token);

        if (updateResult.IsSuccess)
            return Ok();
        
        return BadRequest(updateResult.Error);
    }

    [HttpGet("fmuApiUpdate/{token}")]
    public async Task<IActionResult> DownloadFmuApiUpdate(string token)
    {
        var rangeFrom = ParseBytesRangeFrom(Request.Headers.Range.ToString());
        var updateData = await _managerService.FmuApiUpdate(token, rangeFrom);

        if (updateData.IsFailure)
            return RangeFailureOrBadRequest(updateData.Error);

        var download = updateData.Value;
        const string fileName = "update.zip";
        var contentType = string.IsNullOrWhiteSpace(download.ContentType)
            ? "application/octet-stream"
            : download.ContentType;

        Response.Headers.AcceptRanges = "bytes";

        if (download.IsPartial)
        {
            Response.StatusCode = StatusCodes.Status206PartialContent;
            Response.Headers.ContentRange = $"bytes {download.From}-{download.To}/{download.TotalLength}";
        }

        return new FileStreamResult(download.Content, contentType)
        {
            FileDownloadName = fileName
        };
    }

    /// <summary>
    /// Читает начало диапазона из заголовка Range вида bytes={from}- или bytes={from}-{to}.
    /// </summary>
    private static long? ParseBytesRangeFrom(string? rangeHeader)
    {
        if (string.IsNullOrWhiteSpace(rangeHeader)
            || !rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            return null;

        var spec = rangeHeader["bytes=".Length..];
        var dash = spec.IndexOf('-');
        if (dash <= 0)
            return null;

        return long.TryParse(spec[..dash], out var from) ? from : null;
    }

    /// <summary>
    /// Преобразует отказ докачки в 416, остальные ошибки — в 400.
    /// </summary>
    private IActionResult RangeFailureOrBadRequest(string error)
    {
        const string prefix = SoftwareUpdateFileDownload.RangeNotSatisfiableCode + ":";
        if (!error.StartsWith(prefix, StringComparison.Ordinal))
            return BadRequest(error);

        if (long.TryParse(error[prefix.Length..], out var totalLength) && totalLength > 0)
            Response.Headers.ContentRange = $"bytes */{totalLength}";

        return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
    }
}