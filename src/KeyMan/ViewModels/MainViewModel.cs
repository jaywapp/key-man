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
        AddCommand = new AsyncRelayCommand(AddAsync);
        CopyCommand = new AsyncRelayCommand(CopyAsync, () => SelectedEntry is not null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedEntry is not null);
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
            foreach (var entry in await _metadataStore.LoadAsync())
            {
                Entries.Add(entry);
            }

            UpdateEmptyState();
            StatusText = $"{Entries.Count}개 항목";
        }
        catch (Exception exception)
        {
            ReportError("저장된 목록을 불러오지 못했습니다.", exception);
        }
    }

    private async Task AddAsync()
    {
        var credential = _dialogService.ShowAddCredential();
        if (credential is null)
        {
            return;
        }

        try
        {
            _credentialStore.Save(credential.Entry.Id, credential.Secret);
            Entries.Add(credential.Entry);
            try
            {
                await _metadataStore.SaveAsync(Entries);
            }
            catch
            {
                Entries.Remove(credential.Entry);
                _credentialStore.Delete(credential.Entry.Id);
                throw;
            }

            UpdateEmptyState();
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
            await _clipboardService.CopyWithAutoClearAsync(secret, TimeSpan.FromSeconds(30));
            StatusText = "클립보드에서 민감값을 삭제했습니다.";
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
        if (entry is null || !_dialogService.ConfirmDelete(entry.Name))
        {
            return;
        }

        try
        {
            Entries.Remove(entry);
            try
            {
                await _metadataStore.SaveAsync(Entries);
                _credentialStore.Delete(entry.Id);
            }
            catch
            {
                Entries.Add(entry);
                await _metadataStore.SaveAsync(Entries);
                throw;
            }

            UpdateEmptyState();
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

    private void ReportError(string message, Exception exception)
    {
        StatusText = message;
        _dialogService.ShowError(message, exception);
    }
}
