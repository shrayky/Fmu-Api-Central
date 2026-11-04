import { shouldLoadMore } from '../../utils/infiniteScroll.js';

const ROW_HEIGHT = 34;
const PAGE_SIZE = 50;

export class VetisPagedTable {
    constructor({ id, fetchPage, onOpen }) {
        this.id = id;
        this._fetchPage = fetchPage;
        this._onOpen = onOpen;
        this._filters = {};
        this._generation = 0;
        this._skip = 0;
        this._hasMore = false;
        this._loading = false;
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
        this._skip = 0;
        this._hasMore = false;
        this._loading = false;
        table.clearAll();
        this._showProgress(table);

        await this._loadNext();
    }

    async _loadNext() {
        const generation = this._generation;
        this._loading = true;

        try {
            const page = await this._fetchPage(this._filters, this._skip, PAGE_SIZE);
            if (generation !== this._generation) {
                return;
            }

            this._table()?.parse(page.rows);
            this._skip += page.rows.length;
            this._hasMore = page.hasMore;
        } catch (error) {
            if (generation === this._generation) {
                this._hasMore = false;
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
