import { pollingManager } from '../../services/PollingManager.js';
import vetisService from '../../services/vetisService.js';
import { jobStatusHtml } from '../../utils/vetisFormat.js';

const STATUS_ID = "vetisJobsStatus";
const POLL_ID = "vetisJobs";
const POLL_MS = 3000;

export function createVetisJobsTab() {
    return {
        header: "Фоновые задания",
        body: {
            id: STATUS_ID,
            view: "template",
            scroll: "y",
            borderless: true,
            template: "Загрузка статуса…",
        },
    };
}

export function canPaintJobStatus(view) {
    return !!view && !view.$destructed;
}

export function startVetisJobsWatch() {
    pollingManager.register(POLL_ID, refreshVetisJobs, POLL_MS, { autoStart: true });
}

export function stopVetisJobsWatch() {
    pollingManager.unregister(POLL_ID);
}

async function refreshVetisJobs() {
    if (!canPaintJobStatus($$(STATUS_ID)))
        return;

    try {
        const jobs = await vetisService.jobs();
        paint(jobStatusHtml(jobs));
    } catch (error) {
        paint(jobStatusHtml(null, error.message));
    }
}

function paint(html) {
    const view = $$(STATUS_ID);
    if (!canPaintJobStatus(view))
        return;

    view.define("template", `<div style="padding:12px 16px;line-height:1.7;white-space:normal;">${html}</div>`);
    view.refresh();
}
