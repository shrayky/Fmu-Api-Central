using Domain.Configuration.Options;

namespace Configuration.Migrations;

public static class BotAlertSettingsToNestedMigration
{
    /// <summary>
    /// Переносит устаревшие плоские поля настроек оповещений во вложенные объекты LocalModuleAlerts и TsPiotAlerts.
    /// при миграции на версию 1.6
    /// </summary>
    public static bool Apply(TelegramBotSetting bot)
    {
        if (bot == null)
            return false;

        var migrated = false;

        bot.LocalModuleAlerts ??= new LocalModuleAlertSettings();
        bot.TsPiotAlerts ??= new TsPiotAlertSettings();

#pragma warning disable CS0618 // Устаревшие поля используются только для миграции
        if (bot.LegacyLocalModuleVersionAlert != null)
        {
            bot.LocalModuleAlerts.VersionAlert = bot.LegacyLocalModuleVersionAlert;
            bot.LegacyLocalModuleVersionAlert = null;
            migrated = true;
        }

        if (bot.LegacyLocalModuleDaysWithoutSynchronization.HasValue)
        {
            bot.LocalModuleAlerts.DaysWithoutSynchronization = bot.LegacyLocalModuleDaysWithoutSynchronization.Value;
            bot.LegacyLocalModuleDaysWithoutSynchronization = null;
            migrated = true;
        }

        if (bot.LegacyTsPiotStatusAlertEnabled.HasValue)
        {
            bot.TsPiotAlerts.StatusAlertEnabled = bot.LegacyTsPiotStatusAlertEnabled.Value;
            bot.LegacyTsPiotStatusAlertEnabled = null;
            migrated = true;
        }

        if (bot.LegacyTsPiotLicenseAlertEnabled.HasValue)
        {
            bot.TsPiotAlerts.LicenseAlertEnabled = bot.LegacyTsPiotLicenseAlertEnabled.Value;
            bot.LegacyTsPiotLicenseAlertEnabled = null;
            migrated = true;
        }

        if (bot.LegacyTsPiotLicenseAlertDays.HasValue)
        {
            bot.TsPiotAlerts.LicenseAlertDays = bot.LegacyTsPiotLicenseAlertDays.Value;
            bot.LegacyTsPiotLicenseAlertDays = null;
            migrated = true;
        }

        if (bot.LegacyTsPiotVersionAlert != null)
        {
            bot.TsPiotAlerts.VersionAlert = bot.LegacyTsPiotVersionAlert;
            bot.LegacyTsPiotVersionAlert = null;
            migrated = true;
        }
#pragma warning restore CS0618

        return migrated;
    }
}
