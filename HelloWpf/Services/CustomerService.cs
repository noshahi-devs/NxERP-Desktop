using HelloWpf.Data;
using HelloWpf.Models;

namespace HelloWpf.Services;

public sealed class CustomerService
{
    private readonly ICustomerRepository _repository;

    public CustomerService(ICustomerRepository repository)
    {
        _repository = repository;
    }

    public CustomerPageResult GetPage(string? search, int pageNumber, int pageSize)
        => _repository.GetPage(search, pageNumber, pageSize);

    public Customer? GetByCode(string code)
        => string.IsNullOrWhiteSpace(code) ? null : _repository.GetByCode(code);

    public void Upsert(Customer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.Code))
        {
            throw new ArgumentException("Customer code is required.");
        }

        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            throw new ArgumentException("Customer name is required.");
        }

        _repository.Upsert(customer);
    }

    public bool Delete(string code)
        => !string.IsNullOrWhiteSpace(code) && _repository.Delete(code);
}
