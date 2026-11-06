using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using KeyMan.Infrastructure;
using KeyMan.Models;
using KeyMan.Services;

namespace KeyMan.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly MetadataStore _metadataStore;
    private readonly WindowsCredentialStore _credentialStore;
    private readonly IDialogService _dialogService;
    private readonly IClipboardService _clipboardService;
    private string _searchText = string.Empty;
    private string _statusText = "준비됨";
    private CredentialEntry? _selectedEntry;
    private bool _isEmpty = true;
    private bool _isLoaded;

    public MainViewModel(
        MetadataStore metadataStore,
        WindowsCredentialStore credentialStore,
        IDialogService dialogService,
        IClipboardService clipboardService)
    {
        _metadataStore = metadataStore;
        _credentialStore = credentialStore;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        EntriesView = CollectionViewSource.GetDefaultView(Entries);
        EntriesView.Filter = MatchesSearch;

        InitializeCommand = new AsyncRelayCommand(InitializeAsync);
        AddCommand = new AsyncRelayCommand(AddAsync, () => _isLoaded);
        CopyCommand = new AsyncRelayCommand(CopyAsync, () => SelectedEntry is not null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => _isLoaded && SelectedEntry is not null);
    }

    public ObservableCollection<CredentialEntry> Entries { get; } = [];
    public ICollectionView EntriesView { get; }
    public AsyncRelayCommand InitializeCommand { get; }
    public AsyncRelayCommand AddCommand { get; }
    public AsyncRelayCommand CopyCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                EntriesView.Refresh();
                UpdateEmptyState();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public CredentialEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                CopyCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public void Dispose() => _clipboardService.Dispose();

    private async Task InitializeAsync()
    {
        try
        {
            RefreshEntries(await _metadataStore.LoadAsync());
            _isLoaded = true;
            AddCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            StatusText = $"{Entries.Count}개 항목";
        }
        catch (Exception exception)
        {
            ReportError("저장된 목록을 불러오지 못했습니다.", exception);
        }
    }

    private async Task AddAsync()
    {
        if (!_isLoaded)
        {
            return;
        }

        var credential = _dialogService.ShowAddCredential();
        if (credential is null)
        {
            return;
        }

        try
        {
            var entries = await _metadataStore.MutateAsync(transaction =>
            {
                if (transaction.Entries.Any(entry =>
                    entry.Name.Equals(credential.Entry.Name, StringComparison.CurrentCultureIgnoreCase) &&
                    entry.Service.Equals(credential.Entry.Service, StringComparison.CurrentCultureIgnoreCase) &&
                    entry.Environment.Equals(credential.Entry.Environment, StringComparison.CurrentCultureIgnoreCase)))
                {
                    throw new InvalidOperationException("같은 이름·서비스·환경의 항목이 이미 있습니다.");
                }

                transaction.OnRollback(() => _credentialStore.Delete(credential.Entry.Id));
                _credentialStore.Save(credential.Entry.Id, credential.Secret);
                transaction.Entries.Add(credential.Entry);
            });
            RefreshEntries(entries);
            StatusText = $"'{credential.Entry.Name}' 저장됨";
        }
        catch (Exception exception)
        {
            ReportError("키를 저장하지 못했습니다.", exception);
        }
    }

    private async Task CopyAsync()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        try
        {
            var secret = _credentialStore.Read(SelectedEntry.Id);
            StatusText = "클립보드에 복사됨 · 30초 후 자동 삭제";
            var cleared = await _clipboardService.CopyWithAutoClearAsync(secret, TimeSpan.FromSeconds(30));
            StatusText = cleared
                ? "클립보드에서 민감값을 삭제했습니다."
                : "클립보드 내용이 바뀌어 그대로 유지했습니다.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ReportError("키를 복사하지 못했습니다.", exception);
        }
    }

    private async Task DeleteAsync()
    {
        var entry = SelectedEntry;
        if (!_isLoaded || entry is null || !_dialogService.ConfirmDelete(entry.Name))
        {
            return;
        }

        try
        {
            var entries = await _metadataStore.MutateAsync(transaction =>
            {
                var current = transaction.Entries.SingleOrDefault(item => item.Id == entry.Id)
                    ?? throw new InvalidOperationException("다른 작업에서 이미 삭제한 항목입니다. 앱을 다시 열어 목록을 갱신하세요.");
                string? previousSecret = null;
                try
                {
                    previousSecret = _credentialStore.Read(current.Id);
                }
                catch (Win32Exception exception) when (exception.NativeErrorCode == 1168)
                {
                }

                if (previousSecret is not null)
                {
                    transaction.OnRollback(() => _credentialStore.Save(current.Id, previousSecret));
                }

                _credentialStore.Delete(current.Id);
                transaction.Entries.Remove(current);
            });
            RefreshEntries(entries);
            StatusText = $"'{entry.Name}' 삭제됨";
        }
        catch (Exception exception)
        {
            ReportError("키를 삭제하지 못했습니다.", exception);
        }
    }

    private bool MatchesSearch(object item)
    {
        if (item is not CredentialEntry entry)
        {
            return false;
        }

        var search = SearchText.Trim();
        return string.IsNullOrEmpty(search)
            || entry.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || entry.Service.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || entry.Environment.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void UpdateEmptyState() => IsEmpty = EntriesView.IsEmpty;

    private void RefreshEntries(IReadOnlyList<CredentialEntry> entries)
    {
        var selectedId = SelectedEntry?.Id;
        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(entry);
        }

        SelectedEntry = Entries.FirstOrDefault(entry => entry.Id == selectedId);
        UpdateEmptyState();
    }

    private void ReportError(string message, Exception exception)
    {
        StatusText = message;
        _dialogService.ShowError(message, exception);
    }
}
