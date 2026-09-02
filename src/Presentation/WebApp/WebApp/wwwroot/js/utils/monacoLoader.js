let monacoLoadPromise = null;

export function loadMonaco() {
    if (window.monaco) {
        return Promise.resolve(window.monaco);
    }

    if (monacoLoadPromise) {
        return monacoLoadPromise;
    }

    monacoLoadPromise = new Promise((resolve, reject) => {
        const vsPath = `${window.location.origin}/lib/monaco/vs`;

        const startEditor = () => {
            window.require.config({
                paths: { vs: vsPath }
            });
            window.require(["vs/editor/editor.main"], () => {
                resolve(window.monaco);
            }, reject);
        };

        if (window.require && typeof window.require.config === "function") {
            startEditor();
            return;
        }

        const script = document.createElement("script");
        script.src = "/lib/monaco/vs/loader.js";
        script.onload = startEditor;
        script.onerror = () => reject(new Error("Не удалось загрузить Monaco"));
        document.head.appendChild(script);
    });

    return monacoLoadPromise;
}
