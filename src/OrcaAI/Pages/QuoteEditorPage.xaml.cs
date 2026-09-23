using OrcaAI.Infrastructure;
using OrcaAI.Models;
using OrcaAI.ViewModels;

namespace OrcaAI.Pages;

public partial class QuoteEditorPage : ContentPage, IQueryAttributable
{
    private readonly QuoteEditorViewModel _viewModel;
    private bool _loaded;
    private string? _quoteId;

    public QuoteEditorPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<QuoteEditorViewModel>();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _quoteId = query.TryGetValue("quoteId", out var value)
            ? Convert.ToString(value)
            : null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;

        _loaded = true;
        await _viewModel.LoadAsync(_quoteId);
    }

    private void OnRemoveItemClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: QuoteItem item })
            _viewModel.RemoveItem(item);
    }
}
