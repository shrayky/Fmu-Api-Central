import { pollingManager } from '../../services/PollingManager.js';
import etrnService from '../../services/etrnService.js';
import { jobStatusHtml } from '../../utils/vetisFormat.js';

const STATUS_ID = "etrnJobsStatus";
const POLL_ID = "etrnJobs";
const POLL_MS = 3000;

export function createEtrnJobsTab() {
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

export function startEtrnJobsWatch() {
    pollingManager.register(POLL_ID, refreshEtrnJobs, POLL_MS, { autoStart: true });
}

export function stopEtrnJobsWatch() {
    pollingManager.unregister(POLL_ID);
}

async function refreshEtrnJobs() {
    if (!canPaintJobStatus($$(STATUS_ID)))
        return;

    try {
        const jobs = await etrnService.jobs();
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
