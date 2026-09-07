using CSharpFunctionalExtensions;

namespace Domain.Authentication.Interfaces;

public interface IAgentNonceService
{
    Result Consume(string nonce, long unixTimestamp);
}
