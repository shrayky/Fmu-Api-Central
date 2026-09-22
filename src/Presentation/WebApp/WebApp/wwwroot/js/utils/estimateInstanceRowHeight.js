const LINE_HEIGHT = 20;
const CELL_PADDING = 16;
const ITEM_GAP = 16;
const LOCAL_LINES = 2;
const TSP_BASE_LINES = 3;
const NAME_WRAP_CHARS = 20;

function columnHeight(items, itemHeight) {
    if (!items.length) {
        return 0;
    }

    const content = items.reduce((sum, item) => sum + itemHeight(item), 0);
    const gaps = Math.max(0, items.length - 1) * ITEM_GAP;

    return content + gaps;
}

function tspItemHeight(item) {
    const name = String(item?.name ?? "").trim();
    const extraLine = name.length > NAME_WRAP_CHARS ? 1 : 0;

    return (TSP_BASE_LINES + extraLine) * LINE_HEIGHT;
}

export function estimateInstanceRowHeight(record, minHeight = 36) {
    const localModules = record.localModules ?? [];
    const tsPiots = record.TsPiots ?? record.tsPiots ?? [];
    const localHeight = columnHeight(localModules, () => LOCAL_LINES * LINE_HEIGHT);
    const tspHeight = columnHeight(tsPiots, tspItemHeight);
    const contentHeight = Math.max(localHeight, tspHeight);
    const padding = contentHeight > 0 ? CELL_PADDING : 0;

    return Math.max(minHeight, contentHeight + padding);
}
