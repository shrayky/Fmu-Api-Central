import { shouldLoadMore } from '../../utils/infiniteScroll.js';

const ROW_HEIGHT = 34;
const PAGE_SIZE = 50;

/**
 * Следующий skip обычной таблицы: на число показанных строк.
 */
export function nextOffset(page, current) {
    return current + (page?.rows?.length ?? 0);
}

/**
 * Закладка остатков. Пустая начинает список с начала, иначе _find продолжается без skip.
 */
export function nextBookmark(page) {
    return String(page?.bookmark || "");
}

export class VetisPagedTable {
    constructor({ id, fetchPage, onOpen, bookmark = false }) {
        this.id = id;
        this._fetchPage = fetchPage;
        this._onOpen = onOpen;
        this._useBookmark = bookmark;
        this._filters = {};
        this._generation = 0;
        this._skip = 0;
        this._bookmark = "";
        this._hasMore = false;
        this._fromStart = false;
        this._loading = false;
        this._pendingReload = false;
    }

    config(columns) {
        return {
            view: "datatable",
            id: this.id,
            columns,
            rowHeight: ROW_HEIGHT,
            scrollX: false,
            select: "row",
            on: {
                onScrollY: () => this._loadMoreIfNeeded(),
                onItemDblClick: (cell) => this._onOpen(this._table().getItem(cell.row)),
            },
        };
    }

    async reload(filters) {
        const table = this._table();
        if (!table) {
            return;
        }

        this._filters = filters;
        this._generation++;
        this._loading = false;
        this._pendingReload = true;
        this._fromStart = true;
        this._showProgress(table);

        await this._loadNext();
    }

    async _loadNext() {
        const generation = this._generation;
        const fromStart = this._fromStart;
        this._loading = true;

        try {
            const cursor = this._useBookmark
                ? (fromStart ? "" : this._bookmark)
                : (fromStart ? 0 : this._skip);
            const page = await this._fetchPage(this._filters, cursor, PAGE_SIZE);
            if (generation !== this._generation) {
                return;
            }

            const table = this._table();
            if (this._pendingReload) {
                table?.clearAll();
                this._pendingReload = false;
            }

            table?.parse(page.rows);
            if (this._useBookmark)
                this._bookmark = nextBookmark(page);
            else
                this._skip = nextOffset(page, fromStart ? 0 : this._skip);
            this._hasMore = page.hasMore;
            this._fromStart = false;
        } catch (error) {
            if (generation === this._generation) {
                this._pendingReload = false;
                this._fromStart = false;
                webix.message({ text: error.message, type: "error" });
            }
            return;
        } finally {
            if (generation === this._generation) {
                this._loading = false;
                this._hideProgress(this._table());
            }
        }

        this._showEmptyState();
        this._loadMoreIfNeeded();
    }

    _loadMoreIfNeeded() {
        const table = this._table();
        if (!table) {
            return;
        }

        const needed = shouldLoadMore({
            scrollTop: table.getScrollState().y,
            viewHeight: table.$height,
            rowHeight: ROW_HEIGHT,
            rowCount: table.count(),
            hasMore: this._hasMore,
            loading: this._loading,
        });

        if (needed) {
            this._loadNext();
        }
    }

    _showProgress(table) {
        if (!table.showProgress)
            webix.extend(table, webix.ProgressBar);

        table.showProgress({ type: "icon" });
    }

    _hideProgress(table) {
        if (table?.hideProgress)
            table.hideProgress();
    }

    _showEmptyState() {
        const table = this._table();
        if (!table) {
            return;
        }

        webix.extend(table, webix.OverlayBox);
        if (table.count() === 0) {
            table.showOverlay("Нет данных");
        } else {
            table.hideOverlay();
        }
    }

    _table() {
        return $$(this.id);
    }
}
