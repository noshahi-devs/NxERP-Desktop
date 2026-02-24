using HelloWpf.Models;

namespace HelloWpf.Data;

public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Customer> _byCode = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryCustomerRepository()
    {
        SeedDemoData();
    }

    public CustomerPageResult GetPage(string? search, int pageNumber, int pageSize)
    {
        lock (_sync)
        {
            IEnumerable<Customer> query = _byCode.Values;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c =>
                    c.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.Contact.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var total = query.Count();
            var safePage = Math.Max(1, pageNumber);
            var safeSize = Math.Max(10, pageSize);

            var items = query
                .OrderByDescending(c => c.UpdatedAt)
                .Skip((safePage - 1) * safeSize)
                .Take(safeSize)
                .Select(Clone)
                .ToList();

            return new CustomerPageResult(items, total, safePage, safeSize);
        }
    }

    public Customer? GetByCode(string code)
    {
        lock (_sync)
        {
            return _byCode.TryGetValue(code, out var customer) ? Clone(customer) : null;
        }
    }

    public void Upsert(Customer customer)
    {
        lock (_sync)
        {
            var code = customer.Code.Trim();
            customer.Code = code;
            customer.UpdatedAt = DateTime.UtcNow;
            _byCode[code] = Clone(customer);
        }
    }

    public bool Delete(string code)
    {
        lock (_sync)
        {
            return _byCode.Remove(code.Trim());
        }
    }

    private void SeedDemoData()
    {
        var random = new Random(42);
        var types = new[] { "Retail", "Corporate", "Insurance" };

        for (var i = 1; i <= 5000; i++)
        {
            var customer = new Customer
            {
                Code = $"CUS-{i:00000}",
                Name = $"Customer {i:00000}",
                Type = types[i % types.Length],
                Contact = $"03{random.Next(10, 49)}-{random.Next(1000000, 9999999)}",
                OpeningDate = DateTime.Today.AddDays(-i % 365),
                OpeningBalance = random.Next(0, 15000),
                IsActive = true,
                UpdatedAt = DateTime.UtcNow.AddMinutes(-i)
            };

            _byCode[customer.Code] = customer;
        }

        _byCode["CUS-1002"] = new Customer
        {
            Code = "CUS-1002",
            Name = "City Clinic",
            Type = "Corporate",
            Contact = "0300-1234567",
            OpeningDate = new DateTime(2026, 2, 19),
            OpeningBalance = 2150,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Customer Clone(Customer source)
    {
        return new Customer
        {
            Code = source.Code,
            Name = source.Name,
            Type = source.Type,
            Contact = source.Contact,
            OpeningDate = source.OpeningDate,
            OpeningBalance = source.OpeningBalance,
            IsActive = source.IsActive,
            UpdatedAt = source.UpdatedAt
        };
    }
}
