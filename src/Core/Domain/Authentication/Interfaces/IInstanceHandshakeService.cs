using CSharpFunctionalExtensions;
using Domain.Authentication.Dto;

namespace Domain.Authentication.Interfaces;

public interface IInstanceHandshakeService
{
    Task<Result<AgentAccessToken>> Handshake(AgentHandshakeRequest request);
}
