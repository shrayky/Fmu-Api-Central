export function visibleMenuItems(menuItems, vetisEnabled, etrnEnabled = false) {
    return Object.values(menuItems).filter((item) => {
        if (item.requiresVetis && !vetisEnabled)
            return false;

        if (item.requiresEtrn && !etrnEnabled)
            return false;

        return true;
    });
}

/** Позиция среди уже показанных пунктов: считаем показанные пункты, стоящие в полном меню раньше нужного. */
export function menuInsertIndex(menuItems, shownIds, id) {
    const order = Object.values(menuItems).map((item) => item.id);
    return order
        .slice(0, order.indexOf(id))
        .filter((itemId) => shownIds.includes(itemId))
        .length;
}
