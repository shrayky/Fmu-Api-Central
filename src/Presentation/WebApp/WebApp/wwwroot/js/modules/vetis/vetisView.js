import vetisService from '../../services/vetisService.js';
import { VetisIncomingTab } from './vetisIncomingTab.js';
import { VetisApplicationsTab } from './vetisApplicationsTab.js';
import { createVetisJobsTab, startVetisJobsWatch, stopVetisJobsWatch } from './vetisJobsTab.js';

export default async function createVetisView(id) {
    $$("toolbarLabel").setValue("Fmu-Api-Central: Ветис");

    const organizations = await vetisService.filters();
    const counts = vetisService.notAcceptedCounts().catch(() => null);
    const incoming = new VetisIncomingTab(organizations);
    const applications = new VetisApplicationsTab(organizations);

    // Данные грузим после того, как Router построит интерфейс: до этого таблиц ещё нет.
    // Роутер может закрыть раздел раньше 10 мс: таймер иначе стартует опрос на следующей странице.
    let startTimer = 0;
    const page = {
        id,
        on: {
            onDestruct() {
                clearTimeout(startTimer);
                stopVetisJobsWatch();
            }
        },
        rows: [{
            view: "tabview",
            cells: [
                { header: "Входящие документы", body: incoming.config() },
                { header: "Заявки", body: applications.config() },
                createVetisJobsTab(),
            ],
        }],
    };
    startTimer = setTimeout(() => {
        incoming.load();
        applications.load();
        startVetisJobsWatch();
        counts.then((organizations) => incoming.applyCounts(organizations));
    }, 10);
    return page;
}
