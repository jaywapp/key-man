using System.Windows;
using KeyMan.ViewModels;

namespace KeyMan;

public partial class AddCredentialWindow : Window
{
    private readonly AddCredentialViewModel _viewModel;

    public AddCredentialWindow(AddCredentialViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.SaveRequested += ViewModel_SaveRequested;
        Loaded += (_, _) => NameTextBox.Focus();
        Closed += (_, _) => viewModel.SaveRequested -= ViewModel_SaveRequested;
    }

    private void SecretPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Secret = SecretPasswordBox.Password;
    }

    private void ViewModel_SaveRequested(object? sender, EventArgs e)
    {
        DialogResult = true;
    }
}
