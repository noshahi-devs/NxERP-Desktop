using HelloWpf.Data;
using HelloWpf.Models;

namespace HelloWpf.Services;

public sealed class SupplierService
{
    private readonly ISupplierRepository _repository;

    public SupplierService(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public SupplierPageResult GetPage(string? search, int pageNumber, int pageSize) => _repository.GetPage(search, pageNumber, pageSize);
    public Supplier? GetByCode(string code) => string.IsNullOrWhiteSpace(code) ? null : _repository.GetByCode(code);

    public void Upsert(Supplier supplier)
    {
        if (string.IsNullOrWhiteSpace(supplier.Code)) throw new ArgumentException("Supplier code is required.");
        if (string.IsNullOrWhiteSpace(supplier.Name)) throw new ArgumentException("Supplier name is required.");
        _repository.Upsert(supplier);
    }

    public bool Delete(string code) => !string.IsNullOrWhiteSpace(code) && _repository.Delete(code);
}
