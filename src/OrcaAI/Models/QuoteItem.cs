using System.Text.Json.Serialization;
using OrcaAI.Infrastructure;

namespace OrcaAI.Models;

public sealed class QuoteItem : ObservableObject
{
    private string _description = string.Empty;
    private decimal _quantity = 1;
    private decimal _unitPrice;

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value ?? string.Empty);
    }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, Math.Max(0, value)))
                OnPropertyChanged(nameof(Total));
        }
    }

    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (SetProperty(ref _unitPrice, Math.Max(0, value)))
                OnPropertyChanged(nameof(Total));
        }
    }

    [JsonIgnore]
    public decimal Total => Quantity * UnitPrice;
}
