using HelloWpf.Models;

namespace HelloWpf.Data;

public sealed record CategoryPageResult(IReadOnlyList<Category> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public interface ICategoryRepository
{
    CategoryPageResult GetPage(string? search, int pageNumber, int pageSize);
    Category? GetByCode(string code);
    void Upsert(Category category);
    bool Delete(string code);
}
