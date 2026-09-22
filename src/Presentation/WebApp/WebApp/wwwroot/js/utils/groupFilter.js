export const ALL_GROUPS_VALUE = "__all__";
export const WITHOUT_GROUP_VALUE = "__without_group__";

/**
 * Значение выпадающего списка групп по сохранённому отбору.
 * Отбор «без группы» хранится флагом withoutGroup, а не идентификатором группы.
 */
export function toGroupSelectValue(filters) {
    if (filters?.withoutGroup) {
        return WITHOUT_GROUP_VALUE;
    }

    return filters?.groupId ? String(filters.groupId) : ALL_GROUPS_VALUE;
}

/**
 * Отбор по значению выпадающего списка групп.
 */
export function toGroupFilter(value) {
    if (value === WITHOUT_GROUP_VALUE) {
        return { groupId: "", withoutGroup: true };
    }

    return {
        groupId: value === ALL_GROUPS_VALUE || !value ? "" : String(value),
        withoutGroup: false
    };
}
