/** Пока открыта команда установки, Esc закрывает её, а не карточку с несохранённым вводом. */
export function instanceCardCancelTarget(commandWindowOpen) {
    return commandWindowOpen ? "command" : "card";
}
