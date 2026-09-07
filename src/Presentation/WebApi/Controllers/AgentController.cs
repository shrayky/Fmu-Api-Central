using Domain.Authentication;
using Domain.Authentication.Dto;
using Domain.Authentication.Interfaces;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.SoftwareUpdateFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace WebApi.Controllers;

[Route("api/Agent")]
[ApiController]
public class AgentController : ControllerBase
{
    private readonly IInstanceHandshakeService _handshake;
    private readonly IInstanceManagerService _managerService;

    public AgentController(IInstanceHandshakeService handshake, IInstanceManagerService managerService)
    {
        _handshake = handshake;
        _managerService = managerService;
    }

    [AllowAnonymous]
    [HttpPost("handshake")]
    public async Task<IActionResult> Handshake([FromBody] AgentHandshakeRequest request)
    {
        var result = await _handshake.Handshake(request);
        if (result.IsSuccess)
            return Ok(result.Value);

        if (result.Error == "Неверная подпись"
            || result.Error == "Секретный ключ не задан"
            || result.Error.Contains("не найден", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(result.Error);

        return BadRequest(result.Error);
    }

    [Authorize(Policy = AgentAuthClaims.Policy)]
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] JsonDocument packet)
    {
        var instanceId = InstanceId();
        if (instanceId is null)
            return Unauthorized();

        if (!TokenMatches(packet, instanceId))
            return Forbid();

        var updateResult = await _managerService.UpdateFmuApiInstanceInformation(packet.RootElement.GetRawText());
        return updateResult.IsSuccess ? Ok(updateResult.Value) : BadRequest(updateResult.Error);
    }

    [Authorize(Policy = AgentAuthClaims.Policy)]
    [HttpGet("settings")]
    public async Task<IActionResult> Settings()
    {
        var instanceId = InstanceId();
        if (instanceId is null)
            return Unauthorized();

        return Ok(await _managerService.InstanceSettings(instanceId));
    }

    [Authorize(Policy = AgentAuthClaims.Policy)]
    [HttpPut("settings/updated")]
    public async Task<IActionResult> SettingsUpdated()
    {
        var instanceId = InstanceId();
        if (instanceId is null)
            return Unauthorized();

        var updateResult = await _managerService.SettingsUploaded(instanceId);
        return updateResult.IsSuccess ? Ok() : BadRequest(updateResult.Error);
    }

    [Authorize(Policy = AgentAuthClaims.Policy)]
    [HttpGet("fmuApiUpdate")]
    public async Task<IActionResult> DownloadFmuApiUpdate()
    {
        var instanceId = InstanceId();
        if (instanceId is null)
            return Unauthorized();

        var rangeFrom = ParseBytesRangeFrom(Request.Headers.Range.ToString());
        var updateData = await _managerService.FmuApiUpdate(instanceId, rangeFrom);

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

    private string? InstanceId()
        => User.FindFirst(AgentAuthClaims.InstanceId)?.Value
           ?? User.FindFirst(ClaimTypes.Name)?.Value;

    private static bool TokenMatches(JsonDocument packet, string instanceId)
    {
        if (!packet.RootElement.TryGetProperty("token", out var tokenElement))
            return false;

        return string.Equals(tokenElement.GetString(), instanceId, StringComparison.Ordinal);
    }

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
