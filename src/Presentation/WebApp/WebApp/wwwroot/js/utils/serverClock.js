const STAMP = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?(Z|[+-]\d{2}:\d{2})$/;

/**
 * Подпись идёт по смещению сервера. getHours браузера показал бы другой пояс.
 */
export function serverClockText(iso, elapsedMs = 0) {
    const match = STAMP.exec(String(iso ?? "").trim());
    if (!match)
        return "";

    const offset = match[8] === "Z" ? 0 : zoneMinutes(match[8]);
    if (offset == null)
        return "";

    const fraction = match[7] ? Number(match[7].padEnd(3, "0").slice(0, 3)) : 0;
    const utc = Date.UTC(
        Number(match[1]),
        Number(match[2]) - 1,
        Number(match[3]),
        Number(match[4]),
        Number(match[5]),
        Number(match[6]),
        fraction) - offset * 60000;
    const wall = new Date(utc + Math.max(0, elapsedMs) + offset * 60000);
    const pad = (value) => String(value).padStart(2, "0");
    return `${pad(wall.getUTCDate())}.${pad(wall.getUTCMonth() + 1)}.${wall.getUTCFullYear()} ${pad(wall.getUTCHours())}:${pad(wall.getUTCMinutes())}:${pad(wall.getUTCSeconds())}`;
}

function zoneMinutes(zone) {
    const match = /^([+-])(\d{2}):(\d{2})$/.exec(zone);
    if (!match)
        return null;

    const minutes = Number(match[2]) * 60 + Number(match[3]);
    if (Number(match[2]) > 23 || Number(match[3]) > 59)
        return null;

    return (match[1] === "-" ? -1 : 1) * minutes;
}
