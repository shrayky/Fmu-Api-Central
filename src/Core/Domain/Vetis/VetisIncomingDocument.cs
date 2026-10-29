using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Domain.Entitys.Interfaces;

namespace Domain.Vetis;

public sealed class VetisIncomingVsd
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("issueNumber")]
    public string IssueNumber { get; set; } = "";

    [JsonPropertyName("issueDate")]
    public string IssueDate { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "";

    [JsonPropertyName("volume")]
    public string Volume { get; set; } = "";

    [JsonPropertyName("consignorName")]
    public string ConsignorName { get; set; } = "";

    [JsonPropertyName("consigneeName")]
    public string ConsigneeName { get; set; } = "";

    [JsonPropertyName("sourceNumber")]
    public string SourceNumber { get; set; } = "";

    [JsonPropertyName("sourceDate")]
    public string SourceDate { get; set; } = "";

    [JsonPropertyName("sourceType")]
    public string SourceType { get; set; } = "";

    [JsonPropertyName("receivedAt")]
    public string ReceivedAt { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";

    [JsonPropertyName("rawXml")]
    public string RawXml { get; set; } = "";
}

public sealed class VetisIncomingDocumentEntity : IHaveStringId
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = "";

    [JsonPropertyName("enterpriseGuid")]
    public string EnterpriseGuid { get; set; } = "";

    [JsonPropertyName("number")]
    public string Number { get; set; } = "";

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("loose")]
    public bool Loose { get; set; }

    [JsonPropertyName("consignor")]
    public string Consignor { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("sortAt")]
    public long SortAt { get; set; }

    [JsonPropertyName("receivedAt")]
    public string ReceivedAt { get; set; } = "";

    [JsonPropertyName("vsdCount")]
    public int VsdCount { get; set; }

    [JsonPropertyName("acceptedCount")]
    public int AcceptedCount { get; set; }

    [JsonPropertyName("vsds")]
    public List<VetisIncomingVsd> Vsds { get; set; } = [];

    /// <summary>
    /// ВСД одной накладной образуют один документ. Без накладной ВСД группируются по дате оформления и отправителю.
    /// </summary>
    public static List<VetisIncomingDocumentEntity> Group(
        string organizationId,
        string enterpriseGuid,
        IEnumerable<VetisIncomingVsd> vsds)
    {
        var documents = new Dictionary<string, VetisIncomingDocumentEntity>();
        foreach (var vsd in vsds)
        {
            var loose = string.IsNullOrWhiteSpace(vsd.SourceNumber);
            var date = DatePart(loose ? vsd.IssueDate : vsd.SourceDate);
            var consignor = loose ? vsd.ConsignorName.Trim() : "";
            var key = loose
                ? $"{organizationId}|{enterpriseGuid}|loose|{date}|{consignor}"
                : $"{organizationId}|{enterpriseGuid}|{vsd.SourceNumber.Trim()}|{date}|{vsd.SourceType.Trim()}";
            if (!documents.TryGetValue(key, out var document))
            {
                document = new VetisIncomingDocumentEntity
                {
                    Id = "vetis-in-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant())))[..32].ToLowerInvariant(),
                    OrganizationId = organizationId,
                    EnterpriseGuid = enterpriseGuid,
                    Number = loose ? "" : vsd.SourceNumber.Trim(),
                    Date = date,
                    Type = vsd.SourceType.Trim(),
                    Loose = loose,
                    Consignor = loose ? consignor : vsd.ConsignorName.Trim()
                };
                documents[key] = document;
            }

            document.Vsds.Add(vsd);
        }

        foreach (var document in documents.Values)
            document.Refresh();

        return documents.Values.ToList();
    }

    public void Merge(IEnumerable<VetisIncomingVsd> fresh)
    {
        foreach (var vsd in fresh)
        {
            var index = Vsds.FindIndex(item => string.Equals(item.Uuid, vsd.Uuid, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                Vsds[index] = vsd;
            else
                Vsds.Add(vsd);
        }

        Refresh();
    }

    private void Refresh()
    {
        Status = VetisIncomingStatus.Of(Vsds.Select(item => item.Status));
        VsdCount = Vsds.Count;
        AcceptedCount = Vsds.Count(item => VetisIncomingStatus.IsUtilized(item.Status));
        ReceivedAt = LatestReceipt();
        SortAt = VetisDateTime.TryParse(ReceivedAt, out var instant) ? instant.ToUnixTimeSeconds() : 0;
    }

    /// <summary>
    /// ВСД переехал в другой документ: в прежнем его больше нет, иначе в списке он виден дважды.
    /// </summary>
    public bool Drop(IEnumerable<string> uuids)
    {
        var set = new HashSet<string>(uuids ?? [], StringComparer.OrdinalIgnoreCase);
        if (set.Count == 0 || Vsds.RemoveAll(item => set.Contains(item.Uuid)) == 0)
            return false;

        Refresh();
        return true;
    }

    private string LatestReceipt()
    {
        string best = "";
        DateTimeOffset? moment = null;
        foreach (var vsd in Vsds)
        {
            if (!VetisDateTime.TryParse(vsd.ReceivedAt, out var instant) || (moment != null && instant <= moment))
                continue;

            moment = instant;
            best = vsd.ReceivedAt;
        }

        return best;
    }

    private static string DatePart(string? value)
    {
        var text = (value ?? "").Trim();
        return text.Length >= 10 && text[4] == '-' && text[7] == '-' ? text[..10] : text;
    }
}
