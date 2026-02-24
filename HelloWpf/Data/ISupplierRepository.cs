using HelloWpf.Models;

namespace HelloWpf.Data;

public sealed record SupplierPageResult(IReadOnlyList<Supplier> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public interface ISupplierRepository
{
    SupplierPageResult GetPage(string? search, int pageNumber, int pageSize);
    Supplier? GetByCode(string code);
    void Upsert(Supplier supplier);
    bool Delete(string code);
}
