using System.Net;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Etrn;

namespace EtrnSaby.Tests;

internal sealed record SabyCall(string Method, string Body, string Url, string? SessionHeader)
{
    public JsonElement Params()
    {
        using var document = JsonDocument.Parse(Body);
        return document.RootElement.TryGetProperty("params", out var parameters)
            ? parameters.Clone()
            : default;
    }

    public string Param(string name)
    {
        var parameters = Params();
        return parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty(name, out var value)
            ? value.ToString()
            : "";
    }
}

internal sealed class SabyHandler : HttpMessageHandler
{
    public List<SabyCall> Calls { get; } = [];

    public Func<SabyCall, int, string> Respond { get; set; } = (_, _) => Result("true");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(body);
        var method = document.RootElement.GetProperty("method").GetString() ?? "";
        var session = request.Headers.TryGetValues("X-SBISSessionID", out var values) ? values.First() : null;
        var call = new SabyCall(method, body, request.RequestUri?.ToString() ?? "", session);
        Calls.Add(call);

        var response = Respond(call, Calls.Count(item => item.Method == method));
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }

    public static string Result(string json) => $$"""{"jsonrpc":"2.0","result":{{json}},"id":"1"}""";

    public static string Error(string message) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            error = new { code = -32000, message },
            id = "1"
        });
}

internal sealed class SabyHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public SabyHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

internal sealed class FakeSabyAuthenticator : ISabyAuthenticator
{
    private int _logins;

    public int Logins => _logins;

    public Task<Result<string>> LoginAsync(EtrnOperatorContext context, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success($"session-{Interlocked.Increment(ref _logins)}"));
}

internal sealed class FakeSabyCertificates : ISabyCertificateDirectory
{
    public Result<SabyCertificateInfo> Find(string? serialNumber) =>
        Result.Success(new SabyCertificateInfo { Thumbprint = "THUMB-1" });
}

internal sealed class FakeSignaturePort : IEtrnSignaturePort
{
    public List<EtrnSignatureRequest> Requests { get; } = [];

    public string Signature { get; set; } = "SIGNED";

    public Task<Result<string>> SignAsync(EtrnSignatureRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Result.Success(Signature));
    }
}
