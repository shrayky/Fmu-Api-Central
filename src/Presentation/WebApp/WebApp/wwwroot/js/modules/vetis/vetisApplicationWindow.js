import vetisService from '../../services/vetisService.js';
import { formatUnixMoment, operationLabel, requestStateText } from '../../utils/vetisFormat.js';
import { infoTemplate, openVetisWindow } from './vetisWindow.js';

export async function openApplicationWindow(row) {
    let application;
    try {
        application = await vetisService.application(row.id);
    } catch (error) {
        webix.message({ text: error.message, type: "error" });
        return;
    }

    openVetisWindow({
        id: "vetisApplicationWindow",
        title: `${operationLabel(application.operation)} · ${formatUnixMoment(application.createdAt)}`,
        width: 1400,
        height: 720,
        body: {
            rows: [
                infoTemplate([
                    ["Параметры", application.parameters],
                    ["Организация", application.organizationName],
                    ["Предприятие", application.enterpriseName],
                    ["Ответ", requestStateText(application)],
                    ["Статус заявки", application.status],
                    ["Ошибка", application.error],
                ], 130),
                {
                    cols: [
                        xmlPane("Запрос", application.requestXml),
                        { view: "resizer" },
                        xmlPane("Ответ", application.responseXml),
                    ],
                },
            ],
        },
    });
}

function xmlPane(title, xml) {
    return {
        rows: [
            { view: "template", type: "header", template: title },
            { view: "monaco", language: "xml", readOnly: true, value: xml },
        ],
    };
}
