namespace Domain.Entitys.InstanceGroup;

public static class CheckerSettingsAttachment
{
    public const string Name = "settings.json";
    public const int MaxBytes = 2 * 1024 * 1024;

    // RequestSizeLimit считает весь запрос, вместе с полями multipart.
    // Запас нужен, чтобы файл ровно на пределе дошёл до проверки и получил 400, а не 413.
    public const int RequestBytes = MaxBytes + 64 * 1024;

    public const string InvalidJsonMessage = "Файл настроек должен быть JSON-объектом";
    public const string TooLargeMessage = "Файл настроек больше 2 МБ";
    public const string GroupNotFoundMessage = "Группа не найдена";
    public const string ReadFailedMessage = "Не удалось прочитать настройки группы";
}

public readonly record struct CheckerSettingsRead(bool Exists, byte[] Content);
