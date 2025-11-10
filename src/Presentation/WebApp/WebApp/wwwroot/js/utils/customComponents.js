// Создаем файл для/home/arseniiz/c_sharp_prj/FrontolConfigurator кастомных компонентов
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