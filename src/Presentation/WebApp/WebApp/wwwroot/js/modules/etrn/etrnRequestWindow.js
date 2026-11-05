import etrnService from '../../services/etrnService.js';
import { etrnMethodLabel, etrnResponseText, formatUnixMoment } from '../../utils/etrnFormat.js';
import { infoTemplate, openVetisWindow } from '../vetis/vetisWindow.js';

export async function openEtrnRequestWindow(row) {
    let request;
    try {
        request = await etrnService.request(row.id);
    } catch (error) {
        webix.message({ text: error.message, type: "error" });
        return;
    }

    openVetisWindow({
        id: "etrnRequestWindow",
        title: `${etrnMethodLabel(request.method)} · ${formatUnixMoment(request.createdAt)}`,
        width: 1400,
        height: 720,
        body: {
            rows: [
                infoTemplate([
                    ["Запрос", etrnMethodLabel(request.method)],
                    ["Параметры", request.parameters],
                    ["Организация", request.organizationName],
                    ["Оператор", request.operatorName],
                    ["Ответ", etrnResponseText(request)],
                    ["Ошибка", request.error],
                ], 130),
                {
                    cols: [
                        jsonPane("Запрос", request.requestBody),
                        { view: "resizer" },
                        jsonPane("Ответ", request.responseBody),
                    ],
                },
            ],
        },
    });
}

function jsonPane(title, json) {
    return {
        rows: [
            { view: "template", type: "header", template: title },
            { view: "monaco", language: "json", readOnly: true, value: json },
        ],
    };
}
