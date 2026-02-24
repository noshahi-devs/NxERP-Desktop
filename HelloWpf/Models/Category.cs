namespace HelloWpf.Models;

public sealed class Category
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Medicine";
    public string ParentCategory { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Today;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
