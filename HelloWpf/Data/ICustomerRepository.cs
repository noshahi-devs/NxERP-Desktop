using HelloWpf.Models;

namespace HelloWpf.Data;

public sealed record CustomerPageResult(IReadOnlyList<Customer> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public interface ICustomerRepository
{
    CustomerPageResult GetPage(string? search, int pageNumber, int pageSize);
    Customer? GetByCode(string code);
    void Upsert(Customer customer);
    bool Delete(string code);
}
