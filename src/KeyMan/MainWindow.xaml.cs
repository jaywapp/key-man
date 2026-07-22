using System.Windows;
using KeyMan.Services;
using KeyMan.ViewModels;

namespace KeyMan;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(
            new MetadataStore(),
            new WindowsCredentialStore(),
            new WpfDialogService(this),
            new WpfClipboardService());
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.InitializeCommand.Execute(null);
        Closed += (_, _) => _viewModel.Dispose();
    }
}
