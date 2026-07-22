using KeyMan.Infrastructure;
using KeyMan.Models;

namespace KeyMan.ViewModels;

public sealed class AddCredentialViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string _service = string.Empty;
    private string _environment = "Development";
    private string _secret = string.Empty;
    private DateTime? _expirationDate;
    private string _notes = string.Empty;
    private string _errorMessage = string.Empty;

    public AddCredentialViewModel()
    {
        SaveCommand = new RelayCommand(Save);
    }

    public event EventHandler? SaveRequested;

    public RelayCommand SaveCommand { get; }
    public IReadOnlyList<string> Environments { get; } = ["Development", "Staging", "Production"];

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Service
    {
        get => _service;
        set => SetProperty(ref _service, value);
    }

    public string Environment
    {
        get => _environment;
        set => SetProperty(ref _environment, value);
    }

    public string Secret
    {
        get => _secret;
        set => SetProperty(ref _secret, value);
    }

    public DateTime? ExpirationDate
    {
        get => _expirationDate;
        set => SetProperty(ref _expirationDate, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public CredentialEntry CreateEntry() => new()
    {
        Id = Guid.NewGuid(),
        Name = Name.Trim(),
        Service = Service.Trim(),
        Environment = Environment.Trim(),
        Notes = Notes.Trim(),
        ExpiresOn = ExpirationDate is { } expiration ? DateOnly.FromDateTime(expiration) : null,
        CreatedAt = DateTimeOffset.Now
    };

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "이름을 입력해 주세요.";
            return;
        }

        if (string.IsNullOrEmpty(Secret))
        {
            ErrorMessage = "키 또는 토큰을 입력해 주세요.";
            return;
        }

        ErrorMessage = string.Empty;
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }
}
