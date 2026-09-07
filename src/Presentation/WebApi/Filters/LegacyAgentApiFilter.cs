using Domain.Configuration.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebApi.Filters;

public class LegacyAgentApiFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var parameters = context.HttpContext.RequestServices.GetRequiredService<IParametersService>();
        var settings = await parameters.Current();

        if (!settings.Security.AllowLegacyAgentApi)
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
