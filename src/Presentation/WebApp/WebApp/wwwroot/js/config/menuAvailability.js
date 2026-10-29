export function visibleMenuItems(menuItems, vetisEnabled) {
    return Object.values(menuItems).filter((item) => vetisEnabled || !item.requiresVetis);
}

/** Позиция среди уже показанных пунктов: считаем показанные пункты, стоящие в полном меню раньше нужного. */
export function menuInsertIndex(menuItems, shownIds, id) {
    const order = Object.values(menuItems).map((item) => item.id);
    return order
        .slice(0, order.indexOf(id))
        .filter((itemId) => shownIds.includes(itemId))
        .length;
}
