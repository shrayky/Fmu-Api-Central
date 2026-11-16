/**
 * Чистые преобразования справочника точек: строки таблицы карточки и полезная нагрузка сохранения.
 */

export function salePointsToRows(points) {
    return (points || []).map((point, index) => ({
        id: (point.uid || "").trim() || `new-${index}`,
        uid: (point.uid || "").trim(),
        name: (point.name || "").trim(),
        address: (point.address || "").trim(),
        fiasId: (point.fiasId || "").trim(),
        closed: !!point.closed,
        aliases: (point.aliases || []).map((alias) => ({
            uid: (alias.uid || "").trim(),
            address: (alias.address || "").trim()
        }))
    }));
}

export function salePointsToPayload(rows) {
    return (rows || []).map((row) => ({
        uid: (row.uid || "").trim(),
        name: (row.name || "").trim(),
        address: (row.address || "").trim(),
        fiasId: (row.fiasId || "").trim(),
        closed: !!row.closed,
        aliases: (row.aliases || [])
            .map((alias) => ({ uid: (alias.uid || "").trim(), address: (alias.address || "").trim() }))
            .filter((alias) => alias.address.length > 0)
    }));
}

/** Те же пределы, что проверяет сохранение организации: ошибка видна в окне точки. */
export function salePointFieldError(point) {
    const name = String(point?.name || "").trim();
    if (!name)
        return "Укажите название торговой точки";

    if (name.length > 150)
        return "Название торговой точки: не больше 150 символов";

    const address = String(point?.address || "").trim();
    if (!address)
        return "Укажите адрес торговой точки";

    if (address.length > 250)
        return "Адрес торговой точки: не больше 250 символов";

    if (String(point?.fiasId || "").trim().length > 36)
        return "ФИАС торговой точки: не больше 36 символов";

    if ((point?.aliases || []).some((alias) => String(alias.address || "").trim().length > 250))
        return "Адрес синонима: не больше 250 символов";

    return "";
}

/**
 * Подпись точки в таблице и журнале: имя из справочника плюс пометка закрытой.
 */
export function salePointLabel(point) {
    const name = String(point?.salePointName ?? point?.name ?? "").trim();
    if (!name)
        return "";

    const closed = !!(point?.salePointClosed ?? point?.closed);
    return closed ? `${name} (закрыта)` : name;
}

/**
 * Варианты отбора в журнале: «Все точки», «Без точки», открытые по порядку справочника, затем закрытые.
 */
export function salePointFilterOptions(salePoints) {
    const points = salePoints || [];
    return [
        { id: "", value: "Все точки" },
        { id: "none", value: "Без точки" },
        ...points
            .filter((point) => !point.closed)
            .map((point) => ({ id: point.uid, value: salePointLabel(point) })),
        ...points
            .filter((point) => point.closed)
            .map((point) => ({ id: point.uid, value: salePointLabel(point) }))
    ];
}

/**
 * Варианты выбора точки в карточке документа: закрытых нет, первым идёт «Очистить».
 * id пункта не пустой: Webix иначе подменяет его числом, и на сервер уходил не uid.
 */
export function salePointPickerOptions(salePoints) {
    return [
        { id: "clear", uid: "", value: "Очистить" },
        ...(salePoints || [])
            .filter((point) => !point.closed)
            .map((point) => ({ id: point.uid, uid: point.uid, value: salePointLabel(point) }))
    ];
}

/** Тип улицы или дома перед названием: «ул. Ленина», «д. 5». */
const LEADING_TYPES = [
    "ул", "улица", "пр-кт", "проспект", "пр", "пер", "переулок", "ш", "шоссе",
    "наб", "набережная", "пл", "площадь", "б-р", "бульвар", "туп", "тупик",
    "проезд", "д", "дом", "корп", "корпус", "стр", "строение", "кв", "квартал",
    "мкр", "микрорайон", "г", "город", "п", "пос", "поселок", "с", "село", "ст", "станица"
];

/** Тип после названия: «Свердловская ул.». Дом сюда не входит: «113 Д» — это номер. */
const TRAILING_TYPES = [
    "ул", "улица", "пр-кт", "проспект", "пер", "переулок", "ш", "шоссе",
    "наб", "набережная", "пл", "площадь", "б-р", "бульвар", "туп", "тупик",
    "проезд", "корп", "корпус", "стр", "строение", "квартал", "мкр", "микрорайон"
];

/**
 * Короткое имя точки из полного адреса: последние две части (улица и дом) без типов.
 * «Красноярский край, г. Красноярск, Свердловская ул., д. 113 Д» → «Свердловская 113Д».
 */
export function salePointNameFromAddress(address) {
    const parts = String(address || "")
        .split(",")
        .map((part) => part.trim())
        .filter(Boolean);
    const tail = parts
        .slice(-2)
        .map(stripAddressType)
        .filter(Boolean);
    const name = tail.join(" ").replace(/\s+/g, " ").trim();
    return name.replace(/(\d)\s+([A-Za-zА-Яа-я])$/u, "$1$2");
}

function stripAddressType(part) {
    let text = String(part || "").trim();

    const leading = text.match(/^([A-Za-zА-Яа-я-]+)\.?\s+(.+)$/u);
    if (leading && LEADING_TYPES.includes(leading[1].toLowerCase()))
        text = leading[2].trim();

    const trailing = text.match(/^(.+?)\s+([A-Za-zА-Яа-я-]+)\.?$/u);
    if (trailing && TRAILING_TYPES.includes(trailing[2].toLowerCase()))
        text = trailing[1].trim();

    return text;
}

/**
 * Разбор вставленного списка адресов: каждая непустая строка — новая точка.
 * Одинаковые адреса пропускаются: сравнение без регистра и с обрезкой пробелов,
 * как при сохранении организации. taken — адреса, которые уже есть в таблице.
 * Слишком длинное название или адрес не попадают в points: иначе отказ придёт
 * только при сохранении всей организации. error — текст первой такой строки.
 */
export function salePointsFromAddresses(text, taken = []) {
    const known = new Set(
        (taken || []).map((item) => String(item || "").trim().toLowerCase()).filter(Boolean)
    );
    const points = [];
    let error = "";
    for (const address of String(text || "").split(/\r?\n/).map((line) => line.trim()).filter(Boolean)) {
        const key = address.toLowerCase();
        if (known.has(key))
            continue;

        known.add(key);
        const point = {
            uid: "",
            name: salePointNameFromAddress(address) || address,
            address,
            fiasId: "",
            closed: false,
            aliases: []
        };
        const fieldError = salePointFieldError(point);
        if (fieldError) {
            if (!error)
                error = fieldError;
            continue;
        }

        points.push(point);
    }

    return { points, error };
}
