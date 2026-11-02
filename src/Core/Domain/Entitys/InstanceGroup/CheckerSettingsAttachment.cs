namespace Domain.Entitys.InstanceGroup;

public static class CheckerSettingsAttachment
{
    public const string Name = "settings.json";
    public const string InvalidJsonMessage = "Файл настроек должен быть JSON-объектом";
    public const string GroupNotFoundMessage = "Группа не найдена";
    public const string ReadFailedMessage = "Не удалось прочитать настройки группы";
}

public readonly record struct CheckerSettingsRead(bool Exists, byte[] Content);
