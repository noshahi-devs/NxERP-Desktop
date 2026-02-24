namespace HelloWpf.Models;

public sealed class Customer
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Retail";
    public string Contact { get; set; } = string.Empty;
    public DateTime OpeningDate { get; set; } = DateTime.Today;
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
