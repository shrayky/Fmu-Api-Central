using Application.Tests.Fakes;
using Application.Vetis;
using Domain.Vetis;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class VetisIncomingTransportSweepTests
{
    [Fact]
    public async Task Sweep_снимает_чужой_транспорт_и_удаляет_опустевший_документ()
    {
        var incoming = new MemoryVetisIncoming();
        incoming.Store["a"] = Document("a",
            Vsd("v-in", Shipment("INCOMING", "")),
            Vsd("v-own", Shipment("TRANSPORT", "e1")),
            Vsd("v-foreign", Shipment("TRANSPORT", "e2")));
        incoming.Store["b"] = Document("b", Vsd("v-only", Shipment("TRANSPORT", "e2")));
        var sweep = new VetisIncomingTransportSweep(incoming, NullLogger<VetisIncomingTransportSweep>.Instance);

        var step = (await sweep.SweepBatchAsync("", CancellationToken.None)).Value;

        Assert.True(step.Finished);
        Assert.Equal(["v-in", "v-own"], incoming.Store["a"].Vsds.Select(item => item.Uuid));
        Assert.False(incoming.Store.ContainsKey("b"));
        Assert.Equal(2, incoming.FindCalls);
    }

    private static VetisIncomingDocumentEntity Document(string id, params VetisIncomingVsd[] vsds) => new()
    {
        Id = id,
        OrganizationId = "o1",
        EnterpriseGuid = "e1",
        Date = "2026-10-01",
        Vsds = vsds.ToList()
    };

    private static VetisIncomingVsd Vsd(string uuid, string rawXml) => new()
    {
        Uuid = uuid,
        Status = "CONFIRMED",
        Volume = "10",
        RawXml = rawXml
    };

    private static string Shipment(string type, string consigneeGuid) => consigneeGuid.Length == 0
        ? $"""<vetDocument><vetDType>{type}</vetDType></vetDocument>"""
        : $"""
            <vetDocument>
              <vetDType>{type}</vetDType>
              <certifiedConsignment>
                <consignee><enterprise><guid>{consigneeGuid}</guid></enterprise></consignee>
              </certifiedConsignment>
            </vetDocument>
            """;
}
