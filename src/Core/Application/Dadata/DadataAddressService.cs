using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Dadata;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Dadata;

/// <summary>
/// Сценарий DaData: проверка настроек, вызов порта и разбор ответа. HTTP здесь нет.
/// </summary>
[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class DadataAddressService
{
    public const string DisabledError = DadataMessages.Disabled;
    public const string NoApiKeyError = DadataMessages.NoApiKey;
    public const string EmptyAddressError = DadataMessages.EmptyAddress;

    private readonly IDadataAddressClient _client;
    private readonly IParametersService _parameters;

    public DadataAddressService(IDadataAddressClient client, IParametersService parameters)
    {
        _client = client;
        _parameters = parameters;
    }

    /// <summary>
    /// Дома по адресу для кнопки «Получить». Выключенный флаг и пустой ключ не доходят до сети.
    /// </summary>
    public async Task<Result<IReadOnlyList<DadataHouse>>> Suggest(
        string address,
        CancellationToken cancellationToken)
    {
        var text = (address ?? "").Trim();
        if (text.Length == 0)
            return Result.Failure<IReadOnlyList<DadataHouse>>(EmptyAddressError);

        var settings = (await _parameters.Current()).DadataSettings ?? new();
        if (!settings.Enable)
            return Result.Failure<IReadOnlyList<DadataHouse>>(DisabledError);

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return Result.Failure<IReadOnlyList<DadataHouse>>(NoApiKeyError);

        var suggested = await _client.Suggest(text, cancellationToken);
        if (suggested.IsFailure)
            return Result.Failure<IReadOnlyList<DadataHouse>>(suggested.Error);

        return DadataAddressReader.Houses(suggested.Value);
    }

    /// <summary>
    /// Уверенный ФИАС дома по адресу документа. Пустой результат — стандартизация не нужна или не уверена.
    /// </summary>
    public async Task<Result<DadataCleanOutcome>> Clean(string address, CancellationToken cancellationToken)
    {
        var text = (address ?? "").Trim();
        if (text.Length == 0)
            return Result.Success(DadataCleanOutcome.Empty);

        var settings = (await _parameters.Current()).DadataSettings ?? new();
        if (!settings.CleanEnabled)
            return Result.Success(DadataCleanOutcome.Empty);

        var cleaned = await _client.Clean(text, cancellationToken);
        if (cleaned.IsFailure)
            return Result.Failure<DadataCleanOutcome>(cleaned.Error);

        var fias = DadataAddressReader.ConfidentFias(cleaned.Value);
        if (fias.Length == 0)
            return Result.Success(DadataCleanOutcome.Empty);

        return Result.Success(new DadataCleanOutcome
        {
            FiasId = fias,
            // Каноническую строку берём только у уверенного разбора: по ней ищем точки без ФИАС.
            Address = ((cleaned.Value ?? []).FirstOrDefault()?.Result ?? "").Trim()
        });
    }
}
