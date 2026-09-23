using System.Collections.ObjectModel;
using System.Windows.Input;
using OrcaAI.Infrastructure;
using OrcaAI.Models;
using OrcaAI.Pages;
using OrcaAI.Services;

namespace OrcaAI.ViewModels;

public sealed class QuotesViewModel : BaseViewModel
{
    private readonly IOrcaDataStore _store;
    private List<Quote> _all = [];
    private string _search = string.Empty;

    public QuotesViewModel(IOrcaDataStore store)
    {
        _store = store;
        NewCommand = new AsyncRelayCommand(() => NavigateAsync(nameof(QuoteEditorPage)));
    }

    public ObservableCollection<Quote> Items { get; } = [];
    public ICommand NewCommand { get; }

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value ?? string.Empty))
                ApplyFilter();
        }
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            ClearError();
            _all = (await _store.GetQuotesAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            SetError("Não foi possível carregar os orçamentos agora. Tente novamente.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task EditAsync(Quote quote) =>
        NavigateAsync($"{nameof(QuoteEditorPage)}?quoteId={quote.Id}");

    public async Task DeleteAsync(Quote quote)
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            ClearError();
            await _store.DeleteQuoteAsync(quote.Id);
            _all.RemoveAll(x => x.Id == quote.Id);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            SetError("Não foi possível excluir o orçamento. Tente novamente.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task NavigateAsync(string route)
    {
        try
        {
            ClearError();
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            SetError("Não foi possível abrir o orçamento. Tente novamente.", ex);
        }
    }

    private void ApplyFilter()
    {
        var query = Search.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _all
            : _all.Where(x => x.Number.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                           || x.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                           || x.ClientName.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();

        Items.Clear();
        foreach (var item in filtered)
            Items.Add(item);
    }
}
