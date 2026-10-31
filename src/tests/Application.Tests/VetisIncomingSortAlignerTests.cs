using Application.Tests.Fakes;
using Application.Vetis;
using Domain.Vetis;

namespace Application.Tests;

public class VetisIncomingSortAlignerTests
{
    [Fact]
    public async Task Align_переписывает_только_строки_с_другим_ключом()
    {
        var incoming = new MemoryVetisIncoming();
        incoming.Store["a"] = Document("a", VetisIncomingDocumentEntity.DocumentSortAt("2026-10-01"), "v1");
        incoming.Store["b"] = Document("b", 50, "v2");
        var aligner = new VetisIncomingSortAligner(incoming);

        var step = (await aligner.AlignBatchAsync("", CancellationToken.None)).Value;

        Assert.True(step.Finished);
        Assert.Equal("b", step.AfterId);
        Assert.Equal(1, incoming.FindCalls);
        Assert.Equal(1, incoming.SaveCalls);
        Assert.Equal(VetisIncomingDocumentEntity.DocumentSortAt("2026-10-01"), incoming.Store["b"].SortAt);
        Assert.Equal("v2", incoming.Store["b"].Vsds.Single().Uuid);
        Assert.Equal("v1", incoming.Store["a"].Vsds.Single().Uuid);
    }

    private static VetisIncomingDocumentEntity Document(string id, long sortAt, string uuid) => new()
    {
        Id = id,
        Date = "2026-10-01",
        SortAt = sortAt,
        OrganizationId = "o1",
        EnterpriseGuid = "e1",
        Vsds = [new VetisIncomingVsd { Uuid = uuid, Status = "CONFIRMED", Volume = "10", RawXml = "<vetDocument/>" }]
    };
}
