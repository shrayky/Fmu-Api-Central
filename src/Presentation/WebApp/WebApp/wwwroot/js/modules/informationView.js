import { ServerAddress } from '../utils/net.js';
import { openMarkingCalendarWindow } from './markingCalendarWindow.js';

export default function informationView(id) {
    $$("toolbarLabel").setValue("Fmu-Api-Central: Информация о системе");

    let appVersion = 'Получаю версию сервиса...';
    let server = ServerAddress();

    return {
        id,
        rows: [
            {
                view: "form",
                on:{
                    onViewShow: init(server),
                },
                elements: [
                    {
                        view: "label",
                        id: "appVersion",
                        label: appVersion
                    },
                    {
                        view: "label",
                        id: "lDatabaseEngineInfo",
                        label: "Для работы необходимо скачать и установить базу данных <a href=\"https://couchdb.apache.org\" target=\"_blank\" style=\"color: red\">Apache CouchDb.</a>",
                    },
                    {
                        view: "label",
                        id: "browserRecommendation",
                        label: "Рекомендуется использовать браузер Vivaldi, Edge, Opera, Chrome - в общем все на базе Chromium."
                    },
                     {
                         view: "label",
                         id: "swaggerLink",
                         label: `<a href="${server}/scalar/v1" target="_blank" style=\"color: red">Консоль запросов к api.</a>.`
                     },
                    {
                        view: "label",
                        id: "markingCalendarLink",
                        label: "• &#128197; <a href=\"#\" class=\"marking-calendar-link\" style=\"color: #0088cc\">Календарь внедрения маркировки в рознице</a>"
                    },
                    {},
                ]
            }
        ]
    };
}

function init(server) {

    let endpoint = `${server}/api/configuration/about`;

    webix.ajax().get(endpoint)
        .then(function (dirtyData) {
            let data = dirtyData.json();
            let _appVersion = `Версия ${data.version}, сборка ${data.assembly}`;
            $$("appVersion").setValue(_appVersion);
        });
}

function onInfoLinkClick(event) {
    const calendarLink = event.target.closest("a.marking-calendar-link");
    if (!calendarLink)
        return;

    event.preventDefault();
    openMarkingCalendarWindow();
}

document.addEventListener("click", onInfoLinkClick);