namespace OrcaAI.Models;

public sealed class BusinessProfile
{
    public string BusinessName { get; set; } = "Minha empresa";
    public string OwnerName { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int DefaultValidityDays { get; set; } = 7;
    public decimal DefaultLaborValue { get; set; } = 150m;
}
