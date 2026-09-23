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
        NewCommand = new AsyncRelayCommand(() => Shell.Current.GoToAsync(nameof(QuoteEditorPage)));
    }

    public ObservableCollection<Quote> Items { get; } = [];
    public ICommand NewCommand { get; }

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value))
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
            ErrorMessage = string.Empty;
            _all = (await _store.GetQuotesAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar os orçamentos: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task EditAsync(Quote quote) =>
        Shell.Current.GoToAsync($"{nameof(QuoteEditorPage)}?quoteId={quote.Id}");

    public async Task DeleteAsync(Quote quote)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            ErrorMessage = string.Empty;
            await _store.DeleteQuoteAsync(quote.Id);
            _all.RemoveAll(x => x.Id == quote.Id);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível excluir o orçamento: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
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
