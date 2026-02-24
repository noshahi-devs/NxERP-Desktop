namespace HelloWpf.Models;

public sealed class Supplier
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Distributor";
    public string Contact { get; set; } = string.Empty;
    public DateTime OnboardDate { get; set; } = DateTime.Today;
    public decimal OpeningPayable { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
