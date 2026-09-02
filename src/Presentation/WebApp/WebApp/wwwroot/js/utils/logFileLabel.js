/** Подпись файла лога в combo: yyyy-MM-dd, для ролла по размеру — с номером. */
export function formatLogFileLabel(suffix) {
    const match = /^(\d{4})(\d{2})(\d{2})(\d*)$/.exec(suffix);
    if (!match)
        return suffix;

    const date = `${match[1]}-${match[2]}-${match[3]}`;
    return match[4] ? `${date} (${match[4]})` : date;
}
