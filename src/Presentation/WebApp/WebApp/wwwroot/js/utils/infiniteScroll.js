const ROWS_BEFORE_END = 5;

export function shouldLoadMore({ scrollTop, viewHeight, rowHeight, rowCount, hasMore, loading }) {
    if (!hasMore || loading) {
        return false;
    }

    const hiddenBelow = rowCount * rowHeight - (scrollTop + viewHeight);
    return hiddenBelow <= ROWS_BEFORE_END * rowHeight;
}
