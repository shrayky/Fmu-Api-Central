using Application.Authentication.Services;
using CSharpFunctionalExtensions;
using Domain.Authentication.Dto;
using Domain.Authentication.Interfaces;
using Domain.Dto.Responces;
using Domain.Entitys.Instance;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Interfaces;
using Shared.Strings;

namespace Application.Tests;

public class InstanceHandshakeServiceTests
{
    /// <summary>
    /// Без SecretKey JWT не выдаётся.
    /// </summary>
    [Fact]
    public async Task Handshake_без_секрета_отклоняет()
    {
        var instances = new FakeInstanceRepository();
        instances.Store["n1"] = new InstanceEntity { Id = "n1", SecretKey = "" };
        var sut = CreateSut(instances);

        var result = await sut.Handshake(SignedRequest("n1", "secret"));

        Assert.True(result.IsFailure);
        Assert.Equal("Секретный ключ не задан", result.Error);
    }

    /// <summary>
    /// Неверная подпись не выдаёт JWT.
    /// </summary>
    [Fact]
    public async Task Handshake_неверная_подпись_отклоняет()
    {
        var instances = new FakeInstanceRepository();
        instances.Store["n1"] = new InstanceEntity { Id = "n1", SecretKey = "secret" };
        var sut = CreateSut(instances);
        var request = SignedRequest("n1", "secret") with { Signature = "00" };

        var result = await sut.Handshake(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Неверная подпись", result.Error);
    }

    /// <summary>
    /// Верная подпись выдаёт JWT и пишет HandshakeAtUtc.
    /// </summary>
    [Fact]
    public async Task Handshake_успех_пишет_метку_и_jwt()
    {
        var instances = new FakeInstanceRepository();
        instances.Store["n1"] = new InstanceEntity { Id = "n1", SecretKey = "secret" };
        var tokens = new FakeAgentTokenService();
        var sut = CreateSut(instances, tokens);

        var result = await sut.Handshake(SignedRequest("n1", "secret"));

        Assert.True(result.IsSuccess);
        Assert.Equal("agent-jwt", result.Value.AccessToken);
        Assert.NotNull(instances.Store["n1"].HandshakeAtUtc);
        Assert.Equal(1, tokens.GenerateCalls);
    }

    private static InstanceHandshakeService CreateSut(
        FakeInstanceRepository instances,
        FakeAgentTokenService? tokens = null)
        => new(
            instances,
            new AlwaysOkNonceService(),
            tokens ?? new FakeAgentTokenService(),
            TimeProvider.System);

    private static AgentHandshakeRequest SignedRequest(string token, string secret)
    {
        const long timestamp = 1757251200;
        const string nonce = "nonce-1";
        return new AgentHandshakeRequest
        {
            Token = token,
            Timestamp = timestamp,
            Nonce = nonce,
            Signature = InstanceHmac.Sign(secret, token, timestamp, nonce)
        };
    }

    private sealed class AlwaysOkNonceService : IAgentNonceService
    {
        public Result Consume(string nonce, long unixTimestamp) => Result.Success();
    }

    private sealed class FakeAgentTokenService : ITokenService
    {
        public int GenerateCalls { get; private set; }

        public string GenerateToken(string login) => "access";

        public Result<TokenPair> GenerateTokenPair(string login)
            => Result.Success(new TokenPair("access", "refresh", DateTime.UtcNow.AddHours(1)));

        public Result<TokenPair> RefreshAccessToken(string refreshToken)
            => Result.Success(new TokenPair("access", refreshToken, DateTime.UtcNow.AddHours(1)));

        public bool ValidateRefreshToken(string refreshToken) => true;

        public AgentAccessToken GenerateAgentToken(string instanceId)
        {
            GenerateCalls++;
            return new AgentAccessToken("agent-jwt", DateTime.UtcNow.AddMinutes(15));
        }
    }

    private sealed class FakeInstanceRepository : IInstanceRepository
    {
        public Dictionary<string, InstanceEntity> Store { get; } = new();

        public Task<Result> Update(InstanceEntity instance)
        {
            Store[instance.Id] = instance;
            return Task.FromResult(Result.Success());
        }

        public Task<Result<InstanceEntity>> ByToken(string token)
        {
            return Task.FromResult(Store.TryGetValue(token, out var entity)
                ? Result.Success(entity)
                : Result.Failure<InstanceEntity>($"Не найден fmu-api с токеном {token}"));
        }

        public Task<Result<PaginatedResponse<InstanceEntity>>> List(int pageNumber, int pageSize, InstanceListFilter filter)
            => throw new NotImplementedException();

        public Task<Result<bool>> CreateInstance(InstanceEntity instance) => throw new NotImplementedException();

        public Task<Result<bool>> DeleteInstance(InstanceEntity instance) => throw new NotImplementedException();

        public Task<Result<List<InstanceEntity>>> OfflineInstances(DateTime toDate) => throw new NotImplementedException();

        public Task<Result<List<InstanceEntity>>> All() => Task.FromResult(Result.Success(Store.Values.ToList()));

        public Task<Result<List<InstanceEntity>>> ByGroupIds(IReadOnlyList<string> groupIds) => throw new NotImplementedException();

        public Task<Result> ClearGroupLink(string groupId) => throw new NotImplementedException();
    }
}
