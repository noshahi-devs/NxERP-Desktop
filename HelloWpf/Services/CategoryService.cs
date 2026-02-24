using HelloWpf.Data;
using HelloWpf.Models;

namespace HelloWpf.Services;

public sealed class CategoryService
{
    private readonly ICategoryRepository _repository;

    public CategoryService(ICategoryRepository repository)
    {
        _repository = repository;
    }

    public CategoryPageResult GetPage(string? search, int pageNumber, int pageSize) => _repository.GetPage(search, pageNumber, pageSize);
    public Category? GetByCode(string code) => string.IsNullOrWhiteSpace(code) ? null : _repository.GetByCode(code);

    public void Upsert(Category category)
    {
        if (string.IsNullOrWhiteSpace(category.Code)) throw new ArgumentException("Category code is required.");
        if (string.IsNullOrWhiteSpace(category.Name)) throw new ArgumentException("Category name is required.");
        _repository.Upsert(category);
    }

    public bool Delete(string code) => !string.IsNullOrWhiteSpace(code) && _repository.Delete(code);
}
