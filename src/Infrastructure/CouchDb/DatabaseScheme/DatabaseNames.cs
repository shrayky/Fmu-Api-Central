namespace CouchDb.DatabaseScheme;

public class DatabaseNames
{
    public const string Users = "fmu-api-central-users";
    public const string Instance = "fmu-api-central-instance";
    public const string InstanceGroup = "fmu-api-central-instance-group";
    public const string SettingsSchema = "fmu-api-central-settings-schema";
    public const string Organization = "fmu-api-central-organization";
    public const string SoftwareUpdateFiles = "fmu-api-central-software-update-files";
    public const string MarkCheckingStatistic = "fmu-api-central-mark-checking-statistic";
    public const string GisMtDocuments = "fmu-api-central-gismt-documents";
    public const string GisMtMarks = "fmu-api-central-gismt-marks";
    public const string AlertTemplates = "fmu-api-central-alert-templates";

    public static string[] All() => [Users, Instance, InstanceGroup, SettingsSchema, Organization, SoftwareUpdateFiles, MarkCheckingStatistic, GisMtDocuments, GisMtMarks, AlertTemplates];

    public static string[] ExcludedFromExport() => [Users, SoftwareUpdateFiles];
}
