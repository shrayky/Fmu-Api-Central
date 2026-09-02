using Domain.Entitys.AlertTemplates.Dto;

namespace Application.AlertTemplates;

public static class AlertMessageFormatter
{
    public static string Format(AlertDatasetResult dataset, string fallbackTitle)
    {
        if (!string.IsNullOrWhiteSpace(dataset.Message))
            return dataset.Message;

        var title = string.IsNullOrWhiteSpace(dataset.Title) ? fallbackTitle : dataset.Title;
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(title))
            lines.Add(title);

        foreach (var item in dataset.Items)
        {
            if (!string.IsNullOrWhiteSpace(item))
                lines.Add(item);
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Приводит переносы к формату Telegram GET (%0A), как в текущих C#-оповещениях.
    /// </summary>
    public static string ToTelegramText(string text)
    {
        return text
            .Replace("\r\n", "%0A", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);
    }
}
