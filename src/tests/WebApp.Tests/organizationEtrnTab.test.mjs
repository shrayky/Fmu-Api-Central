import test from "node:test";
import assert from "node:assert/strict";
import { escapeHtml } from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/organization/organizationFormat.js";
import { etrnConnectionsFromTable } from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/organization/organizationEtrnTab.js";

test("escapeHtml экранирует кавычки", () => {
    assert.equal(escapeHtml("\"a\" & <b>"), "&quot;a&quot; &amp; &lt;b&gt;");
    assert.equal(escapeHtml("'a'"), "&#39;a&#39;");
});

test("etrnConnectionsFromTable не отправляет старые секреты Платформы ЭТрН", () => {
    const rows = [{
        id: "ofd-1",
        name: "Площадка",
        type: "ofd",
        enable: true,
        subscriberId: "2775225",
        apiKey: "***",
        clientId: "client",
        clientSecret: "***",
        keycloakUrl: "https://old.example",
        realm: "old",
        apiUrl: "https://api.example",
        incomingFrom: new Date(2026, 8, 1)
    }];
    globalThis.$$ = (id) => id === "etrnConnections"
        ? { getSelectedItem: () => null, serialize: () => rows }
        : null;

    const [connection] = etrnConnectionsFromTable({
        NAMES: { etrnConnections: "etrnConnections" },
        _loadedEtrn: []
    });

    assert.equal(connection.apiKey, undefined);
    assert.equal(connection.clientSecret, undefined);
    assert.equal(connection.keycloakUrl, undefined);
    assert.equal(connection.realm, undefined);
    assert.equal(connection.clientId, "client");
    assert.equal(connection.subscriberId, "2775225");
    assert.equal(connection.apiUrl, "https://api.example");
});
