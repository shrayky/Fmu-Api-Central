import etrnService from '../../services/etrnService.js';
import { EtrnIncomingTab } from './etrnIncomingTab.js';
import { EtrnRequestsTab } from './etrnRequestsTab.js';
import { createEtrnJobsTab, startEtrnJobsWatch, stopEtrnJobsWatch } from './etrnJobsTab.js';

export default async function createEtrnView(id) {
    $$("toolbarLabel").setValue("Fmu-Api-Central: ЭТрН");

    const organizations = await etrnService.filters();
    const incoming = new EtrnIncomingTab(organizations);
    const requests = new EtrnRequestsTab(organizations);

    // Данные грузим после того, как Router построит интерфейс: до этого таблиц ещё нет.
    let startTimer = 0;
    const page = {
        id,
        on: {
            onDestruct() {
                clearTimeout(startTimer);
                stopEtrnJobsWatch();
            }
        },
        rows: [{
            view: "tabview",
            cells: [
                { header: "Входящие документы", body: incoming.config() },
                { header: "Запросы", body: requests.config() },
                createEtrnJobsTab(),
            ],
        }],
    };
    startTimer = setTimeout(() => {
        incoming.load();
        requests.load();
        startEtrnJobsWatch();
    }, 10);
    return page;
}
