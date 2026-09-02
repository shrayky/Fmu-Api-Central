using Domain.Entitys.AlertTemplates;

namespace Application.AlertTemplates;

public static class AlertTemplateDefaults
{
    public static IReadOnlyList<AlertTemplateEntity> All() =>
    [
        Template("check-online-nodes", "Недоступные узлы", CheckOnlineNodes),
        Template("check-lm-status", "Статус локальных модулей", CheckLmStatus),
        Template("check-lm-versions", "Версии локальных модулей", CheckLmVersions),
        Template("check-lm-sync-date", "Синхронизация локальных модулей", CheckLmSyncDate),
        Template("check-ts-piot-status", "Состояние ТС ПИоТ", CheckTsPiotStatus),
        Template("check-ts-piot-license", "Лицензии ТС ПИоТ", CheckTsPiotLicense),
        Template("check-ts-piot-versions", "Версии ТС ПИоТ", CheckTsPiotVersions)
    ];

    private static AlertTemplateEntity Template(string id, string name, string script) => new()
    {
        Id = id,
        Name = name,
        Script = script,
        Enabled = false,
        Scheduler = []
    };

    private const string CheckOnlineNodes =
        """
        const offlineHours = 12;
        const offline = instances.filter(function (n) {
            return n.hoursSinceUpdate >= offlineHours;
        });

        return {
            items: offline.map(function (n) {
                return "🚨<b>" + n.name + "</b> Не в сети!%0A последний обмен: <u>" + n.lastUpdated + "</u>!";
            })
        };
        """;

    private const string CheckLmStatus =
        """
        const offlineHours = 12;
        const items = [];

        instances.forEach(function (n) {
            if (n.hoursSinceUpdate >= offlineHours)
                return;

            (n.localModules || []).forEach(function (lm) {
                if (lm.status === "ready")
                    return;

                var status = lm.status === "" ? "не готов" : lm.status;
                items.push("🚨<b>Локальный модуль в " + n.name + " " + lm.address + "</b>%0A в не рабочем состоянии!%0A статус: <u>" + status + "</u>!");
            });
        });

        return { items: items };
        """;

    private const string CheckLmVersions =
        """
        const minVersion = "4.0.0";
        const offlineHours = 12;
        const items = [];

        instances.forEach(function (n) {
            if (n.hoursSinceUpdate >= offlineHours)
                return;

            (n.localModules || []).forEach(function (lm) {
                if (lm.status !== "ready")
                    return;
                if (!isVersionBelowThreshold(lm.version, minVersion))
                    return;

                items.push("🚨<b>Локальный модуль в " + n.name + " " + lm.address + "</b> устарел!%0A текущая версия: <u>" + lm.version + "</u>!");
            });
        });

        return { items: items };
        """;

    private const string CheckLmSyncDate =
        """
        const daysWithoutSynchronization = 3;
        const offlineHours = 12;
        const toDateTimestamp = Date.parse(now) - daysWithoutSynchronization * 24 * 60 * 60 * 1000;
        const items = [];

        instances.forEach(function (n) {
            if (n.hoursSinceUpdate >= offlineHours)
                return;

            (n.localModules || []).forEach(function (lm) {
                if (lm.status !== "ready" || lm.lastSync >= toDateTimestamp)
                    return;

                var lastSync = new Date(lm.lastSync).toLocaleString();
                items.push("🚨<b>Локальный модуль в " + n.name + " " + lm.address + "</b> давно не обновлялся!%0A последнее обновление: <u>" + lastSync + "</u>%0AПроведите инициализацию!");
            });
        });

        return { items: items };
        """;

    private const string CheckTsPiotStatus =
        """
        const offlineHours = 12;
        const items = [];

        instances.forEach(function (n) {
            if (n.hoursSinceUpdate >= offlineHours)
                return;

            (n.tsPiots || []).forEach(function (ts) {
                if (ts.online)
                    return;

                items.push("🚨<b>ТС ПИоТ " + ts.name + " в " + n.name + " " + ts.address + "</b>%0A не в сети!");
            });
        });

        return { items: items };
        """;

    private const string CheckTsPiotLicense =
        """
        const licenseAlertDays = 7;
        const offlineHours = 12;
        const today = new Date(now);
        today.setHours(0, 0, 0, 0);
        const alertUntil = new Date(today);
        alertUntil.setDate(alertUntil.getDate() + licenseAlertDays);
        const items = [];

        instances.forEach(function (n) {
            if (n.hoursSinceUpdate >= offlineHours)
                return;

            (n.tsPiots || []).forEach(function (ts) {
                if (!ts.licenseActiveTill)
                    return;

                var licenseDate = new Date(ts.licenseActiveTill);
                licenseDate.setHours(0, 0, 0, 0);
                var dateText = licenseDate.toLocaleDateString("ru-RU");

                if (licenseDate < today) {
                    items.push("🚨<b>ТС ПИоТ " + ts.name + " в " + n.name + " " + ts.address + "</b>%0A Лицензия истекла!%0A дата: <u>" + dateText + "</u>!");
                    return;
                }

                if (licenseDate > alertUntil)
                    return;

                var daysLeft = Math.round((licenseDate - today) / (24 * 60 * 60 * 1000));
                items.push("🚨<b>ТС ПИоТ " + ts.name + " в " + n.name + " " + ts.address + "</b>%0A Лицензия истекает через " + daysLeft + " дней!%0A дата: <u>" + dateText + "</u>!");
            });
        });

        return { items: items };
        """;

    private const string CheckTsPiotVersions =
        """
        const minVersion = "1.0.0";
        const offlineHours = 12;
        const items = [];

        instances.forEach(function (n) {
            if (n.hoursSinceUpdate >= offlineHours)
                return;

            (n.tsPiots || []).forEach(function (ts) {
                if (!isVersionBelowThreshold(ts.version, minVersion))
                    return;

                items.push("🚨<b>ТС ПИоТ " + ts.name + " в " + n.name + " " + ts.address + "</b> устарел!%0A текущая версия: <u>" + ts.version + "</u>!");
            });
        });

        return { items: items };
        """;
}
