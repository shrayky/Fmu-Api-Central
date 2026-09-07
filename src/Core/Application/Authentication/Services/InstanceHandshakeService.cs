using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Authentication.Dto;
using Domain.Authentication.Interfaces;
using Domain.Entitys.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shared.Strings;

namespace Application.Authentication.Services;

[AutoRegisterService(ServiceLifetime.Scoped)]
public class InstanceHandshakeService : IInstanceHandshakeService
{
    private readonly IInstanceRepository _instances;
    private readonly IAgentNonceService _nonces;
    private readonly ITokenService _tokens;
    private readonly TimeProvider _timeProvider;

    public InstanceHandshakeService(
        IInstanceRepository instances,
        IAgentNonceService nonces,
        ITokenService tokens,
        TimeProvider timeProvider)
    {
        _instances = instances;
        _nonces = nonces;
        _tokens = tokens;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AgentAccessToken>> Handshake(AgentHandshakeRequest request)
    {
        var entitySearch = await _instances.ByToken(request.Token ?? string.Empty);
        if (entitySearch.IsFailure)
            return Result.Failure<AgentAccessToken>(entitySearch.Error);

        var instance = entitySearch.Value;
        if (string.IsNullOrEmpty(instance.SecretKey))
            return Result.Failure<AgentAccessToken>("Секретный ключ не задан");

        var nonceResult = _nonces.Consume(request.Nonce, request.Timestamp);
        if (nonceResult.IsFailure)
            return Result.Failure<AgentAccessToken>(nonceResult.Error);

        if (!InstanceHmac.Verify(
                instance.SecretKey,
                request.Token ?? string.Empty,
                request.Timestamp,
                request.Nonce ?? string.Empty,
                request.Signature ?? string.Empty))
            return Result.Failure<AgentAccessToken>("Неверная подпись");

        instance.HandshakeAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var updateResult = await _instances.Update(instance);
        if (updateResult.IsFailure)
            return Result.Failure<AgentAccessToken>(updateResult.Error);

        return Result.Success(_tokens.GenerateAgentToken(instance.Id));
    }
}
