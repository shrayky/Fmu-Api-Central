using Domain.GisMt.Entity;

namespace Domain.GisMt.Models;

/// <summary>
/// Результат поиска марок остатка ГИС МТ.
/// </summary>
public class GisMtMarkSearchResult
{
    public List<GisMtMarkEntity> Marks { get; set; } = [];
    public int Count { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public string SearchTerm { get; set; } = string.Empty;
}
