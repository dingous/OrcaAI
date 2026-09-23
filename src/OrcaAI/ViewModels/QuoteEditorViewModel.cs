using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using OrcaAI.Infrastructure;
using OrcaAI.Models;
using OrcaAI.Services;

namespace OrcaAI.ViewModels;

public sealed class QuoteEditorViewModel : BaseViewModel
{
    private readonly IOrcaDataStore _store;
    private readonly IAiQuoteDraftService _ai;
    private readonly IPdfService _pdf;
    private Quote? _loaded;
    private BusinessProfile _profile = new();
    private Client? _selectedClient;
    private string _number = string.Empty;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _notes = string.Empty;
    private decimal _discount;
    private DateTime _validUntil = DateTime.Today.AddDays(7);
    private QuoteStatusOption? _selectedStatus;

    public QuoteEditorViewModel(IOrcaDataStore store, IAiQuoteDraftService ai, IPdfService pdf)
    {
        _store = store;
        _ai = ai;
        _pdf = pdf;
        GenerateDraftCommand = new AsyncRelayCommand(GenerateDraftAsync, () => !IsBusy);
        AddItemCommand = new Command(AddItem, () => !IsBusy);
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(true), () => !IsBusy);
        ShareCommand = new AsyncRelayCommand(ShareAsync, () => !IsBusy);
        StatusOptions =
        [
            new(QuoteStatus.Draft, "Rascunho"),
            new(QuoteStatus.Sent, "Enviado"),
            new(QuoteStatus.Approved, "Aprovado"),
            new(QuoteStatus.Rejected, "Recusado"),
            new(QuoteStatus.Completed, "Concluído")
        ];
        SelectedStatus = StatusOptions[0];
    }

    public ObservableCollection<Client> Clients { get; } = [];
    public ObservableCollection<QuoteItem> Items { get; } = [];
    public IReadOnlyList<QuoteStatusOption> StatusOptions { get; }

    public Client? SelectedClient { get => _selectedClient; set => SetProperty(ref _selectedClient, value); }
    public string Number { get => _number; private set => SetProperty(ref _number, value); }
    public string Title { get => _title; set => SetProperty(ref _title, value ?? string.Empty); }
    public string Description { get => _description; set => SetProperty(ref _description, value ?? string.Empty); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value ?? string.Empty); }
    public DateTime ValidUntil { get => _validUntil; set => SetProperty(ref _validUntil, value); }

    public decimal Discount
    {
        get => _discount;
        set
        {
            if (SetProperty(ref _discount, Math.Max(0, value)))
                RaiseTotals();
        }
    }

    public QuoteStatusOption? SelectedStatus { get => _selectedStatus; set => SetProperty(ref _selectedStatus, value); }
    public string SubtotalText => CurrentSubtotal.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
    public string TotalText
    {
        get
        {
            var subtotal = CurrentSubtotal;
            var discount = Math.Max(0, Discount);
            var total = discount >= subtotal ? 0 : subtotal - discount;
            return total.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
        }
    }

    private decimal CurrentSubtotal
    {
        get
        {
            var total = 0m;
            foreach (var item in Items)
            {
                var value = item.Total;
                if (value <= 0)
                    continue;

                if (total > decimal.MaxValue - value)
                    return decimal.MaxValue;

                total += value;
            }

            return total;
        }
    }

    public ICommand GenerateDraftCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ShareCommand { get; }

    public async Task LoadAsync(string? id)
    {
        SetBusy(true);
        try
        {
            ClearError();
            _profile = await _store.GetBusinessProfileAsync();
            var clients = await _store.GetClientsAsync();
            Clients.Clear();
            foreach (var client in clients)
                Clients.Add(client);

            if (!string.IsNullOrWhiteSpace(id))
            {
                if (!Guid.TryParse(id, out var quoteId))
                {
                    ErrorMessage = "Identificador do orçamento inválido.";
                    return;
                }

                _loaded = await _store.GetQuoteAsync(quoteId);
                if (_loaded is null)
                {
                    ErrorMessage = "Orçamento não encontrado.";
                    return;
                }

                Number = _loaded.Number;
                Title = _loaded.Title;
                Description = _loaded.Description;
                Notes = _loaded.Notes;
                Discount = _loaded.Discount;
                ValidUntil = _loaded.ValidUntil;
                SelectedClient = Clients.FirstOrDefault(x => x.Id == _loaded.ClientId);
                SelectedStatus = StatusOptions.FirstOrDefault(x => x.Value == _loaded.Status) ?? StatusOptions[0];
                ReplaceItems(_loaded.Items);
                return;
            }

            Number = $"ORC-{DateTime.Now:yyMMdd-HHmmssfff}";
            ValidUntil = DateTime.Today.AddDays(Math.Clamp(_profile.DefaultValidityDays, 1, 365));
            SelectedStatus = StatusOptions[0];
            ReplaceItems([]);
        }
        catch (Exception ex)
        {
            SetError("Não foi possível abrir o orçamento agora. Tente novamente.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void RemoveItem(QuoteItem item)
    {
        if (IsBusy)
            return;

        item.PropertyChanged -= OnItemPropertyChanged;
        Items.Remove(item);
        RaiseTotals();
    }

    private async Task GenerateDraftAsync()
    {
        ClearError();
        if (string.IsNullOrWhiteSpace(Description))
        {
            ErrorMessage = "Descreva o serviço antes de gerar o rascunho.";
            return;
        }

        SetBusy(true);
        try
        {
            _profile = await _store.GetBusinessProfileAsync();
            var draft = await _ai.CreateDraftAsync(Description, _profile);
            Title = draft.Title;
            Description = draft.Description;
            ReplaceItems(draft.Items);
        }
        catch (Exception ex)
        {
            SetError("Não foi possível gerar o rascunho agora. Tente novamente.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void AddItem()
    {
        if (IsBusy)
            return;

        var item = new QuoteItem { Description = "Novo item", Quantity = 1 };
        item.PropertyChanged += OnItemPropertyChanged;
        Items.Add(item);
        RaiseTotals();
    }

    private async Task<Quote?> SaveAsync(bool navigateBack, bool manageBusy = true)
    {
        ClearError();

        if (string.IsNullOrWhiteSpace(Number))
        {
            ErrorMessage = "Não foi possível identificar este orçamento. Reabra a tela e tente novamente.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Informe o título do orçamento.";
            return null;
        }

        if (Items.Count == 0)
        {
            ErrorMessage = "Adicione pelo menos um item ao orçamento.";
            return null;
        }

        if (Items.Any(x => string.IsNullOrWhiteSpace(x.Description)))
        {
            ErrorMessage = "Preencha a descrição de todos os itens.";
            return null;
        }

        if (Items.Any(x => x.Quantity <= 0))
        {
            ErrorMessage = "A quantidade dos itens deve ser maior que zero.";
            return null;
        }

        if (Items.Any(x => x.UnitPrice > 0 && x.Quantity > decimal.MaxValue / x.UnitPrice))
        {
            ErrorMessage = "Um dos itens possui quantidade ou valor muito alto.";
            return null;
        }

        var isNewPastValidity = _loaded is null && ValidUntil.Date < DateTime.Today;
        var changedExistingValidityToPast =
            _loaded is not null
            && ValidUntil.Date < DateTime.Today
            && ValidUntil.Date != _loaded.ValidUntil.Date;

        if (isNewPastValidity || changedExistingValidityToPast)
        {
            SetError("A validade do orçamento não pode ser alterada para uma data no passado.");
            return null;
        }

        var subtotal = CurrentSubtotal;
        if (Discount > subtotal)
        {
            ErrorMessage = "O desconto não pode ser maior que o subtotal.";
            return null;
        }

        if (manageBusy)
            SetBusy(true);

        try
        {
            var quote = _loaded ?? new Quote { Number = Number };
            quote.ClientId = SelectedClient?.Id;
            quote.ClientName = SelectedClient?.Name ?? _loaded?.ClientName ?? string.Empty;
            quote.Title = Title.Trim();
            quote.Description = Description.Trim();
            quote.Items = Items.Select(x => new QuoteItem
            {
                Description = x.Description.Trim(),
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice
            }).ToList();
            quote.Discount = Discount;
            quote.Notes = Notes.Trim();
            quote.ValidUntil = ValidUntil;
            quote.Status = SelectedStatus?.Value ?? QuoteStatus.Draft;
            await _store.SaveQuoteAsync(quote);
            _loaded = quote;

            if (navigateBack)
                await Shell.Current.GoToAsync("..");

            return quote;
        }
        catch (Exception ex)
        {
            SetError("Não foi possível salvar o orçamento. Tente novamente.", ex);
            return null;
        }
        finally
        {
            if (manageBusy)
                SetBusy(false);
        }
    }

    private async Task ShareAsync()
    {
        if (IsBusy)
            return;

        SetBusy(true);
        try
        {
            try
            {
                _profile = await _store.GetBusinessProfileAsync();
            }
            catch (Exception ex)
            {
                SetError("Não foi possível carregar os dados da empresa para gerar o PDF.", ex);
                return;
            }

            if (string.IsNullOrWhiteSpace(_profile.BusinessName)
                || string.Equals(_profile.BusinessName.Trim(), "Minha empresa", StringComparison.OrdinalIgnoreCase))
            {
                SetError("Preencha o nome da empresa ou profissional na aba Empresa antes de compartilhar o PDF.");
                return;
            }

            var quote = await SaveAsync(false, manageBusy: false);
            if (quote is null)
                return;

            try
            {
                var path = await _pdf.GenerateQuoteAsync(quote, _profile);
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Compartilhar orçamento",
                    File = new ShareFile(path)
                });
            }
            catch (Exception ex)
            {
                SetError("Não foi possível gerar ou compartilhar o PDF. Tente novamente.", ex);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        IsBusy = value;
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        (GenerateDraftCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ShareCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (AddItemCommand as Command)?.ChangeCanExecute();
    }

    private void ReplaceItems(IEnumerable<QuoteItem> items)
    {
        foreach (var current in Items)
            current.PropertyChanged -= OnItemPropertyChanged;

        Items.Clear();

        foreach (var item in items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
            Items.Add(item);
        }

        RaiseTotals();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => RaiseTotals();

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(TotalText));
    }
}

public sealed record QuoteStatusOption(QuoteStatus Value, string Label);
