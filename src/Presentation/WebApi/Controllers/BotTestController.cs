using Domain.Bot;
using Domain.Configuration.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class BotTestController : ControllerBase
{
    private readonly IParametersService _parametersService;
    private readonly IMessageServiceFactory _factory;
    private readonly IAlertMessageConstructor _alertMessageConstructor;

    public BotTestController(IParametersService parametersService, IMessageServiceFactory factory, IAlertMessageConstructor alertMessageConstructor)
    {
        _parametersService = parametersService;
        _factory = factory;
        _alertMessageConstructor = alertMessageConstructor;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings = await _parametersService.Current();
        var bot = settings.BotSettings;
        if (!bot.IsEnabled)
            return BadRequest("Бот не подключен");

        var service = _factory.For(bot.Provider);
        if (service.IsFailure)
            return BadRequest(service.Error);

        var sendResult = await service.Value.Send(bot.BotToken, bot.ChatId,
            "Халло, мир!%0AСЧАСТЬЕ ДЛЯ ВСЕХ, ДАРОМ, И ПУСТЬ НИКТО НЕ УЙДЁТ ОБИЖЕННЫМ!");
        
        if (sendResult.IsSuccess)
            return Ok();
        
        return BadRequest(sendResult.Error);
    }

    [HttpGet("sendAllerts")]
    public async Task<IActionResult> SendAllerts()
    {
        var settings = await _parametersService.Current();

        if (!settings.BotSettings.IsEnabled)
            return BadRequest("Бот не подключен");

        await _alertMessageConstructor.SendNodesStatus(settings.BotSettings);

        return Ok();
    }
}