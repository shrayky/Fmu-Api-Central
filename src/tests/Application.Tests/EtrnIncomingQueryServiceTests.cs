using Application.Etrn;
using Application.Tests.Fakes;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using Domain.Entitys.Organization;

namespace Application.Tests;

public class EtrnIncomingQueryServiceTests
{
    private const string Address = "Красноярский край, г. Красноярск, пр-кт 60 лет образования СССР, д. 21/5";

    [Fact]
    public async Task Пустой_адрес_берётся_из_сохранённого_титула_и_записывается()
    {
        var incoming = new MemoryEtrnIncoming();
        incoming.Store["doc-1"] = new EtrnIncomingDocumentEntity
        {
            Id = "doc-1",
            OrganizationId = "o1",
            Number = "000014348",
            Date = "14.09.2026",
            Consignor = "ООО Алтайская буренка",
            DeliveryAddress = "",
            Status = EtrnDocumentStatus.Waiting,
            RawXml = Title(Address),
            Operator = new EtrnOperator { ConnectionId = "c1", Type = EtrnOperatorType.Saby, Name = "сбис" }
        };
        var organizations = new MemoryOrganizationRepository();
        organizations.Store["o1"] = new OrganizationEntity { Id = "o1", Name = "ИП" };
        var query = new EtrnIncomingQueryService(
            new ListWithoutTitle(incoming),
            organizations,
            new MemoryEtrnSyncStates());

        var first = await query.ListAsync("", "", "", 0, 50);
        var second = await query.ListAsync("", "", "", 0, 50);

        Assert.True(first.IsSuccess, first.IsFailure ? first.Error : "");
        Assert.Equal(Address, first.Value.Rows[0].DeliveryAddress);
        Assert.Equal(Address, incoming.Store["doc-1"].DeliveryAddress);
        Assert.Equal(Address, second.Value.Rows[0].DeliveryAddress);
        Assert.Equal(1, incoming.SaveCalls);
    }

    [Fact]
    public async Task Пустая_масса_берётся_из_сохранённого_титула_и_записывается()
    {
        var incoming = new MemoryEtrnIncoming();
        incoming.Store["doc-1"] = new EtrnIncomingDocumentEntity
        {
            Id = "doc-1",
            OrganizationId = "o1",
            Number = "ТН-1",
            Date = "10.11.2026",
            Status = EtrnDocumentStatus.Waiting,
            Lines =
            [
                new EtrnCargoLine { Index = 1, Name = "Сыр", Places = 2 },
                new EtrnCargoLine { Index = 2, Name = "Масло", Places = 5, GrossWeight = "1.5" }
            ],
            RawXml = """
                <?xml version="1.0" encoding="utf-8"?>
                <Файл ВерсФорм="5.01">
                  <Документ КНД="1110339">
                    <СодИнфГО НомерТрН="ТН-1" ДатаТрН="10.11.2026">
                      <СвГруз>
                        <ОпГруз НомерГруз="1" НаимГруз="Сыр" КолМестГр="2">
                          <ПлМасГруз МасБрутЗнач="33.382"/>
                        </ОпГруз>
                        <ОпГруз НомерГруз="2" НаимГруз="Масло" КолМестГр="5">
                          <ПлМасГруз МасБрутЗнач="9"/>
                        </ОпГруз>
                      </СвГруз>
                    </СодИнфГО>
                  </Документ>
                </Файл>
                """,
            Operator = new EtrnOperator { ConnectionId = "c1", Type = EtrnOperatorType.Saby, Name = "сбис" }
        };
        var query = new EtrnIncomingQueryService(
            incoming,
            new MemoryOrganizationRepository(),
            new MemoryEtrnSyncStates());

        var first = await query.GetAsync("doc-1");
        var second = await query.GetAsync("doc-1");

        Assert.True(first.IsSuccess, first.IsFailure ? first.Error : "");
        Assert.Equal("33.382", first.Value.Lines[0].GrossWeight);
        Assert.Equal("1.5", first.Value.Lines[1].GrossWeight);
        Assert.Equal("33.382", incoming.Store["doc-1"].Lines[0].GrossWeight);
        Assert.Equal("1.5", incoming.Store["doc-1"].Lines[1].GrossWeight);
        Assert.Equal(1, incoming.SaveCalls);
        Assert.Equal("33.382", second.Value.Lines[0].GrossWeight);
    }

    private static string Title(string address) => $"""
        <?xml version="1.0" encoding="windows-1251"?>
        <Файл>
          <Документ КНД="1110339">
            <СодИнфГО НомерТрН="000014348" ДатаТрН="14.09.2026">
              <СвГП>
                <АдресДостГр>
                  <АдресИнф КодСтр="643" АдрТекст="{address}"/>
                </АдресДостГр>
              </СвГП>
            </СодИнфГО>
          </Документ>
        </Файл>
        """;

    /// <summary>
    /// Список CouchDB не отдаёт rawXml. Карточка по id отдаёт.
    /// </summary>
    private sealed class ListWithoutTitle(MemoryEtrnIncoming inner) : IEtrnIncomingRepository
    {
        public Task<Result<EtrnIncomingDocumentEntity?>> Find(string id) => inner.Find(id);

        public Task<Result> Save(EtrnIncomingDocumentEntity document) => inner.Save(document);

        public async Task<Result<List<EtrnIncomingDocumentEntity>>> List(EtrnIncomingFilter filter, int skip, int limit)
        {
            var rows = await inner.List(filter, skip, limit);
            if (rows.IsSuccess)
            {
                foreach (var row in rows.Value)
                    row.RawXml = "";
            }

            return rows;
        }

        public Task<Result<List<EtrnIncomingDocumentEntity>>> StaleSending(long beforeUnix, int limit) =>
            inner.StaleSending(beforeUnix, limit);
    }
}
