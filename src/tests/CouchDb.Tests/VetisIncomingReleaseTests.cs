using CouchDb.Repositories;
using CSharpFunctionalExtensions;
using Domain.Vetis;

namespace CouchDb.Tests;

public class VetisIncomingReleaseTests
{
    [Fact]
    public async Task Apply_не_теряет_документ_когда_правка_сдвигает_страницу()
    {
        var store = new List<VetisIncomingDocumentEntity>
        {
            Document("keep", ("v1", "2026-10-06T00:00:00+00:00")),
            Document("c", ("v1", "2026-10-05T00:00:00+00:00"), ("stay", "2026-10-04T00:00:00+00:00")),
            Document("a", ("v1", "2026-10-03T00:00:00+00:00")),
            Document("b", ("v1", "2026-10-02T00:00:00+00:00"), ("other", "2026-10-01T00:00:00+00:00"))
        };

        var result = await VetisIncomingRelease.Apply(
            (skip, limit) => Task.FromResult(Result.Success(store.OrderByDescending(item => item.SortAt).Skip(skip).Take(limit).ToList())),
            ["v1"],
            ["keep"],
            id => Task.FromResult(Result.Success(store.SingleOrDefault(item => item.Id == id))),
            _ => Task.FromResult(Result.Success()),
            id =>
            {
                store.RemoveAll(item => item.Id == id);
                return Task.FromResult(Result.Success());
            },
            pageSize: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(["v1"], store.Single(item => item.Id == "keep").Vsds.Select(item => item.Uuid));
        Assert.Equal(["stay"], store.Single(item => item.Id == "c").Vsds.Select(item => item.Uuid));
        Assert.Equal(["other"], store.Single(item => item.Id == "b").Vsds.Select(item => item.Uuid));
        Assert.DoesNotContain(store, item => item.Id == "a");
    }

    private static VetisIncomingDocumentEntity Document(string id, params (string Uuid, string ReceivedAt)[] vsds)
    {
        var document = new VetisIncomingDocumentEntity { Id = id, OrganizationId = "o1", EnterpriseGuid = "e1" };
        document.Merge(vsds.Select(item => new VetisIncomingVsd { Uuid = item.Uuid, ReceivedAt = item.ReceivedAt }));
        return document;
    }
}
