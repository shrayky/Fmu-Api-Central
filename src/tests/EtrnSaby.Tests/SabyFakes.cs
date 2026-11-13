using System.Net;
using System.Text;
using System.Text.Json;
using CertificateSigning;
using CSharpFunctionalExtensions;
using Domain.Etrn;

namespace EtrnSaby.Tests;

internal sealed record SabyCall(string Method, string Body, string Url, string? SessionHeader, string? UserAgent = null)
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

    public string Filter(string name)
    {
        var parameters = Params();
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("Фильтр", out var filter)
            || filter.ValueKind != JsonValueKind.Object
            || !filter.TryGetProperty(name, out var value))
            return "";

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    public string OrganizationInn(string party)
    {
        var parameters = Params();
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("Фильтр", out var filter)
            || filter.ValueKind != JsonValueKind.Object
            || !filter.TryGetProperty("НашаОрганизация", out var organization)
            || organization.ValueKind != JsonValueKind.Object
            || !organization.TryGetProperty(party, out var legal)
            || legal.ValueKind != JsonValueKind.Object
            || !legal.TryGetProperty("ИНН", out var inn))
            return "";

        return inn.GetString() ?? "";
    }

    public string PageSize()
    {
        var parameters = Params();
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("Фильтр", out var filter)
            || filter.ValueKind != JsonValueKind.Object
            || !filter.TryGetProperty("Навигация", out var navigation)
            || navigation.ValueKind != JsonValueKind.Object
            || !navigation.TryGetProperty("РазмерСтраницы", out var size))
            return "";

        return size.GetString() ?? "";
    }

    public string At(params string[] path)
    {
        var current = Params();
        foreach (var name in path)
        {
            current = One(current);
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
                return "";
        }

        current = One(current);
        return current.ValueKind == JsonValueKind.String ? current.GetString() ?? "" : "";
    }

    private static JsonElement One(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0 ? value[0] : value;

    public string DocumentId()
    {
        var parameters = Params();
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("Документ", out var document)
            || document.ValueKind != JsonValueKind.Object
            || !document.TryGetProperty("Идентификатор", out var id))
            return "";

        return id.GetString() ?? "";
    }
}

internal sealed class SabyHandler : HttpMessageHandler
{
    public List<SabyCall> Calls { get; } = [];

    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public Func<SabyCall, int, string> Respond { get; set; } = (_, _) => Result("true");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get)
        {
            var address = request.RequestUri?.ToString() ?? "";
            var fileSession = request.Headers.TryGetValues("X-SBISSessionID", out var sessions) ? sessions.First() : null;
            var agent = request.Headers.TryGetValues("User-Agent", out var agents) ? agents.First() : null;
            Calls.Add(new SabyCall("GET", "", address, fileSession, agent));
            if (string.IsNullOrWhiteSpace(fileSession))
                return new HttpResponseMessage(HttpStatusCode.Forbidden);

            if (!Files.TryGetValue(address, out var file))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(file)
            };
        }

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

    public string? Failure { get; set; }

    public EtrnOperatorContext? Last { get; private set; }

    public Task<Result<string>> LoginAsync(EtrnOperatorContext context, CancellationToken cancellationToken)
    {
        Last = context;
        if (Failure != null)
            return Task.FromResult(Result.Failure<string>(Failure));

        return Task.FromResult(Result.Success($"session-{Interlocked.Increment(ref _logins)}"));
    }
}

internal sealed class FakeSabyCertificates : IOrganizationCertificateDirectory
{
    public Result<OrganizationCertificate> Find(string? serialNumber) =>
        Result.Success(new OrganizationCertificate { Thumbprint = "THUMB-1" });
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
