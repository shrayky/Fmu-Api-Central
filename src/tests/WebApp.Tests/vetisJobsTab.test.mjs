import { test } from "node:test";
import assert from "node:assert/strict";
import { pollingManager } from "../../Presentation/WebApp/WebApp/wwwroot/js/services/PollingManager.js";
import { canPaintJobStatus, startVetisJobsWatch, stopVetisJobsWatch } from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisJobsTab.js";

test("опрос заданий регистрируется в менеджере страниц и снимается с него", () => {
    startVetisJobsWatch();
    try {
        assert.equal(pollingManager.getInfo("vetisJobs")?.isRunning, true);
    }
    finally {
        stopVetisJobsWatch();
    }

    assert.equal(pollingManager.getInfo("vetisJobs"), null);
});

test("уничтоженный шаблон статуса не обновляется", () => {
    assert.equal(canPaintJobStatus(null), false);
    assert.equal(canPaintJobStatus({ $destructed: true }), false);
    assert.equal(canPaintJobStatus({}), true);
});
