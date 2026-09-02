using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Entitys.AlertTemplates.Dto;
using Domain.Entitys.AlertTemplates.Interfaces;
using Jint;
using Microsoft.Extensions.DependencyInjection;

namespace Application.AlertTemplates;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class AlertDatasetScriptExecutor : IAlertDatasetScriptExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Result<AlertDatasetResult> Execute(string script, AlertDatasetContext context)
    {
        if (string.IsNullOrWhiteSpace(script))
            return Result.Failure<AlertDatasetResult>("Скрипт шаблона пуст");

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                instances = context.Instances,
                statistics = context.Statistics,
                now = context.Now.ToUniversalTime().ToString("O"),
                settings = context.Settings
            }, JsonOptions);

            using var engine = new Engine(options =>
            {
                options.TimeoutInterval(TimeSpan.FromSeconds(2));
                options.LimitMemory(8_000_000);
                options.MaxStatements(20_000);
                options.LimitRecursion(64);
            });

            engine.SetValue("isVersionBelowThreshold", new Func<string, string, bool>(IsVersionBelowThreshold));
            engine.SetValue("__payload", payload);
            engine.Execute(
                """
                var __ctx = JSON.parse(__payload);
                var instances = __ctx.instances;
                var statistics = __ctx.statistics;
                var now = __ctx.now;
                var settings = __ctx.settings;
                var result;
                """);

            engine.Execute(
                $$"""
                function __userScript() {
                {{script}}
                }
                var __returned = __userScript();
                var __result = (typeof __returned === "undefined") ? result : __returned;
                """);

            var serialized = engine.Evaluate(
                "typeof __result === 'undefined' || __result === null ? 'null' : JSON.stringify(__result)");

            if (!serialized.IsString())
                return Result.Success(AlertDatasetResult.Empty);

            var json = serialized.AsString();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return Result.Success(AlertDatasetResult.Empty);

            using var document = JsonDocument.Parse(json);
            return Result.Success(ParseDataset(document.RootElement));
        }
        catch (Exception ex)
        {
            return Result.Failure<AlertDatasetResult>($"Ошибка выполнения скрипта шаблона: {ex.Message}");
        }
    }

    /// <summary>
    /// Проверяет, что текущая версия ниже пороговой (суффикс после '-' игнорируется).
    /// </summary>
    public static bool IsVersionBelowThreshold(string currentVersion, string thresholdVersion)
    {
        if (string.IsNullOrEmpty(currentVersion) || string.IsNullOrEmpty(thresholdVersion))
            return false;

        var cleanCurrent = currentVersion.Split('-')[0];
        var cleanThreshold = thresholdVersion.Split('-')[0];

        if (!Version.TryParse(cleanCurrent, out var current) || !Version.TryParse(cleanThreshold, out var threshold))
            return false;

        return current < threshold;
    }

    private static AlertDatasetResult ParseDataset(JsonElement root)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.String:
                return new AlertDatasetResult { Message = root.GetString() ?? string.Empty };

            case JsonValueKind.Array:
                return new AlertDatasetResult { Items = ReadItems(root) };

            case JsonValueKind.Object:
                var title = ReadString(root, "title");
                var message = ReadString(root, "message");
                var items = root.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array
                    ? ReadItems(itemsElement)
                    : [];

                return new AlertDatasetResult
                {
                    Title = title,
                    Message = message,
                    Items = items
                };

            default:
                return AlertDatasetResult.Empty;
        }
    }

    private static string ReadString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var element))
            return string.Empty;

        return element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.ToString();
    }

    private static List<string> ReadItems(JsonElement array)
    {
        var items = new List<string>();

        foreach (var element in array.EnumerateArray())
        {
            var text = ItemToString(element);
            if (!string.IsNullOrWhiteSpace(text))
                items.Add(text);
        }

        return items;
    }

    private static string ItemToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object when element.TryGetProperty("name", out var name) =>
                name.GetString() ?? element.GetRawText(),
            JsonValueKind.Object => element.GetRawText(),
            _ => string.Empty
        };
    }
}
