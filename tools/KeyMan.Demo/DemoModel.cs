using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using KeyMan.Models;

internal abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class DemoCommand(Action action, Func<bool>? enabled = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => enabled?.Invoke() ?? true;
    public void Execute(object? parameter) { if (CanExecute(parameter)) action(); }
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class DemoModel : ObservableModel
{
    private string _searchText = "";
    private string _statusText = "2개 항목 · 가짜 데이터";
    private CredentialEntry? _selected;
    public DemoModel(Action showAdd)
    {
        EntriesView = CollectionViewSource.GetDefaultView(Entries);
        EntriesView.Filter = item => item is CredentialEntry entry &&
            (entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
             entry.Service.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
             entry.Environment.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        AddCommand = new DemoCommand(showAdd);
        CopyCommand = new DemoCommand(() =>
        {
            CopyCount++;
            StatusText = "데모: 실제 클립보드 미사용";
        }, () => SelectedEntry is not null);
        DeleteCommand = new DemoCommand(() => { }, () => false);
    }
    public ObservableCollection<CredentialEntry> Entries { get; } = [];
    public ICollectionView EntriesView { get; }
    public DemoCommand AddCommand { get; }
    public DemoCommand CopyCommand { get; }
    public DemoCommand DeleteCommand { get; }
    public int CopyCount { get; private set; }
    public bool IsEmpty => EntriesView.IsEmpty;
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; EntriesView.Refresh(); Changed(); Changed(nameof(IsEmpty)); }
    }
    public CredentialEntry? SelectedEntry
    {
        get => _selected;
        set { _selected = value; Changed(); CopyCommand.Refresh(); }
    }
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; Changed(); }
    }
    public void Register(CredentialEntry entry)
    {
        Entries.Add(entry);
        Changed(nameof(IsEmpty));
        StatusText = $"'{entry.Name}' 등록 재현 · 메모리의 가짜 데이터";
    }
}

internal sealed class DemoForm(Action<CredentialEntry> save) : ObservableModel
{
    public string Name { get; set; } = "";
    public string Service { get; set; } = "";
    public string Environment { get; set; } = "Development";
    public string Secret { get; set; } = "";
    public DateTime? ExpirationDate { get; set; }
    public string Notes { get; set; } = "";
    public string ErrorMessage => "";
    public string[] Environments { get; } = ["Development", "Staging", "Production"];
    public ICommand SaveCommand => new DemoCommand(() =>
    {
        if (string.IsNullOrWhiteSpace(Name) || Secret != "demo-value-not-a-real-key")
            throw new InvalidOperationException("The demonstration form fixture is incomplete.");
        save(new CredentialEntry
        {
            Id = Guid.Parse("71d39f9d-e923-4ff2-8b3c-167876f36603"), Name = Name, Service = Service,
            Environment = Environment, Notes = Notes,
            ExpiresOn = ExpirationDate is { } date ? DateOnly.FromDateTime(date) : null,
            CreatedAt = new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.Zero)
        });
    });
}
