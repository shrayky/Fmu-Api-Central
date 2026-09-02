import { loadMonaco } from "./monacoLoader.js";

webix.protoUI({
    name: "formtable",
    $allowsClear: true,
    setValue: function(value) {
        this.clearAll();
        if (value) this.parse(value);
    },
    getValue: function() {
        return this.serialize();
    }
}, webix.ui.datatable);

webix.protoUI({
    name: "monaco",
    defaults: {
        language: "javascript",
        theme: "vs-dark",
        extraLib: "",
        extraLibName: "context.d.ts"
    },
    $init: function(config) {
        this._initialValue = config.value || "";
        this._extraLibDisposable = null;
        this.$ready.push(this._initMonaco);
    },
    _initMonaco: function() {
        const container = this.$view;
        const config = this.config;
        container.style.overflow = "hidden";

        loadMonaco().then((monaco) => {
            if (this.$destructed) {
                return;
            }

            monaco.languages.typescript.javascriptDefaults.setCompilerOptions({
                allowNonTsExtensions: true,
                checkJs: true,
                target: monaco.languages.typescript.ScriptTarget.ES5
            });

            this._editor = monaco.editor.create(container, {
                value: this._initialValue || "",
                language: config.language || "javascript",
                theme: config.theme || "vs-dark",
                automaticLayout: true,
                minimap: { enabled: false },
                fontSize: 13,
                tabSize: 4,
                insertSpaces: true,
                wordWrap: "on",
                scrollBeyondLastLine: false,
                suggestOnTriggerCharacters: true,
                quickSuggestions: { other: true, comments: false, strings: false }
            });

            this._applyExtraLib(config.extraLib, config.extraLibName);

            this._editor.onDidChangeModelContent(() => {
                if (this._onChange) {
                    this._onChange();
                }
            });
        }).catch((error) => {
            webix.message({ text: error.message, type: "error" });
        });
    },
    setExtraLib: function(source, name) {
        this.config.extraLib = source || "";
        this.config.extraLibName = name || "context.d.ts";
        this._applyExtraLib(this.config.extraLib, this.config.extraLibName);
    },
    _applyExtraLib: function(source, name) {
        if (this._extraLibDisposable) {
            this._extraLibDisposable.dispose();
            this._extraLibDisposable = null;
        }

        if (!source || !window.monaco) {
            return;
        }

        this._extraLibDisposable = window.monaco.languages.typescript.javascriptDefaults.addExtraLib(
            source,
            name || "context.d.ts"
        );
    },
    setValue: function(value) {
        this._initialValue = value || "";
        if (this._editor) {
            this._editor.setValue(this._initialValue);
        }
    },
    getValue: function() {
        if (this._editor) {
            return this._editor.getValue();
        }
        return this._initialValue || "";
    },
    focus: function() {
        if (this._editor) {
            this._editor.focus();
        }
    },
    getInputNode: function() {
        return this._editor ? this._editor.getDomNode() : null;
    },
    $setSize: function(x, y) {
        const changed = webix.ui.view.prototype.$setSize.call(this, x, y);
        if (changed && this._editor) {
            this._editor.layout();
        }
        return changed;
    },
    destructor: function() {
        if (this._extraLibDisposable) {
            this._extraLibDisposable.dispose();
            this._extraLibDisposable = null;
        }
        if (this._editor) {
            this._editor.dispose();
            this._editor = null;
        }
        webix.ui.view.prototype.destructor.call(this);
    }
}, webix.ui.view);