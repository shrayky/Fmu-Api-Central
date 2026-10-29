using Domain.Configuration.Options;
using Domain.Entitys.Organization;
using Domain.Vetis;

namespace Application.Vetis;

internal static class VetisCallFactory
{
    /// <summary>
    /// Фоновая загрузка работает только с сохранёнными настройками: недостающие поля означают, что организацию ещё не настроили.
    /// </summary>
    public static VetisCall? ForOrganization(VetisSettings server, OrganizationVetisSettings organization)
    {
        var fields = new[]
        {
            organization.ApiKey,
            organization.BasicLogin,
            organization.BasicPassword,
            organization.IssuerId,
            organization.InitiatorLogin
        };
        if (fields.Any(string.IsNullOrWhiteSpace))
            return null;

        return new VetisCall
        {
            Endpoint = VetisEndpoints.Resolve(server.Endpoint),
            ServiceId = VetisEndpoints.ServiceId,
            ApiKey = organization.ApiKey.Trim(),
            BasicLogin = organization.BasicLogin.Trim(),
            BasicPassword = organization.BasicPassword,
            IssuerId = organization.IssuerId.Trim(),
            InitiatorLogin = organization.InitiatorLogin.Trim(),
            TimeoutSeconds = server.TimeoutSeconds > 0 ? server.TimeoutSeconds : 60,
            ResultPollAttempts = server.ResultPollAttempts > 0 ? server.ResultPollAttempts : 8,
            ResultPollDelaySeconds = server.ResultPollDelaySeconds > 0 ? server.ResultPollDelaySeconds : 3
        };
    }
}
