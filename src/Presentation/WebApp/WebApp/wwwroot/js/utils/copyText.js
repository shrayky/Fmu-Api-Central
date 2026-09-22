/**
 * navigator.clipboard нет на HTTP (не secure context).
 */
export async function copyText(text) {
    if (typeof navigator.clipboard?.writeText === "function") {
        await navigator.clipboard.writeText(text);
        return;
    }

    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.setAttribute("readonly", "");
    textarea.style.position = "fixed";
    textarea.style.left = "-9999px";
    document.body.appendChild(textarea);
    textarea.select();

    const copied = document.execCommand("copy");
    document.body.removeChild(textarea);

    if (!copied) {
        throw new Error("Не удалось скопировать в буфер обмена");
    }
}
