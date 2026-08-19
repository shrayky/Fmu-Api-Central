namespace HostApp;

/// <summary>
/// Константы host-службы FMU-API-Central.
/// </summary>
internal static class HostConstants
{
    public const string Manufacture = "Automation";
    public const string AppName = "FmuApiCentral";

    /// <summary>Имя Windows-службы и корневого exe host.</summary>
    public const string ServiceName = "fmu-api-central";

    public const string ServiceDisplayName = "DS:Fmu-Api-Central";
    public const string HostExeName = "fmu-api-central.exe";

    public const string ApiProductName = "fmu-api-central-api";
    public const string WebProductName = "fmu-api-central-web";

    public static readonly string[] ProductNames = [ApiProductName, WebProductName];

    public const int ApiHttpPort = 2579;
    public const int WebHttpPort = 2580;

    /// <summary>Сколько последних версий продукта оставлять на диске.</summary>
    public const int VersionsToKeep = 2;

    public const string LegacyApiServiceName = "DS:Fmu-Api-Central-api";
    public const string LegacyWebServiceName = "DS:Fmu-Api-Central-web";
    public const string LegacyApiFolderName = "FmuApiCentral-api";
    public const string LegacyWebFolderName = "FmuApiCentral-web";
    public const string LegacyApiProcessName = "WebApi";
    public const string LegacyWebProcessName = "WebApp";
}
