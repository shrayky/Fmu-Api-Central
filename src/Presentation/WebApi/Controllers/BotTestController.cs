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
    private readonly IMessageService _messageService;

    public BotTestController(IParametersService parametersService, IMessageService messageService)
    {
        _parametersService = parametersService;
        _messageService = messageService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings = await _parametersService.Current();
        
        if (!settings.BotSettings.IsEnabled)
            return BadRequest("Бот не подключен");

        var sendResult = await _messageService.Send(settings.BotSettings.BotToken, settings.BotSettings.ChatId,
            "Халло, мир!%0A СЧАСТЬЕ ДЛЯ ВСЕХ, ДАРОМ, И ПУСТЬ НИКТО НЕ УЙДЁТ ОБИЖЕННЫЙ!");
        
        if (sendResult.IsSuccess)
            return Ok();
        
        return BadRequest(sendResult.Error);
    }
    
    
    
}