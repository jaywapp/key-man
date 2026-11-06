using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KeyMan;
using KeyMan.Models;
using KeyMan.Services;
using KeyMan.ViewModels;

internal static partial class Program
{
    private static int _checks;

    [STAThread]
    private static int Main(string[] args)
    {
        var directory = Path.Combine(Path.GetTempPath(), "KeyMan.UiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousPath = Environment.GetEnvironmentVariable("KEYMAN_VAULT_PATH");
        Environment.SetEnvironmentVariable("KEYMAN_VAULT_PATH", Path.Combine(directory, "unused.json"));
        var credentialStore = new WindowsCredentialStore();
        var createdIds = new List<Guid>();
        try
        {
            var app = new App();
            app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());

            VerifyLoadFailure(directory);
            VerifyStaleView(directory, credentialStore, createdIds);
            VerifyWindowBindings(directory, args.Length == 2 && args[0] == "--screenshots" ? Path.GetFullPath(args[1]) : null);

            if (args.Length == 2 && args[0] == "--screenshot")
            {
                RenderDemo(directory, Path.GetFullPath(args[1]));
            }

            Console.WriteLine($"PASS: {_checks} GUI state checks (isolated fixture metadata)");
            return 0;
        }
        finally
        {
            foreach (var id in createdIds)
            {
                credentialStore.Delete(id);
            }
            Environment.SetEnvironmentVariable("KEYMAN_VAULT_PATH", previousPath);
            Directory.Delete(directory, true);
        }
    }

    private static void VerifyLoadFailure(string directory)
    {
        var path = Path.Combine(directory, "corrupt.json");
        File.WriteAllText(path, "{");
        var dialogs = new TestDialogs();
        using var viewModel = new MainViewModel(new MetadataStore(path), new WindowsCredentialStore(), dialogs, new TestClipboard());
        Assert(!viewModel.AddCommand.CanExecute(null), "Adding is disabled before loading.");
        viewModel.InitializeCommand.Execute(null);
        PumpUntil(() => dialogs.ErrorCount == 1);
        Assert(!viewModel.AddCommand.CanExecute(null), "Failed loading leaves adding disabled.");
        viewModel.SelectedEntry = Entry("Untrusted fixture");
        Assert(!viewModel.DeleteCommand.CanExecute(null), "Failed loading leaves deletion disabled.");
        viewModel.AddCommand.Execute(null);
        Assert(dialogs.AddCalls == 0, "Disabled add never opens the registration dialog.");
        Assert(File.ReadAllText(path) == "{", "A failed load cannot overwrite the existing file.");
    }

    private static void VerifyStaleView(string directory, WindowsCredentialStore credentialStore, List<Guid> createdIds)
    {
        var path = Path.Combine(directory, "shared.json");
        var original = Entry("Initial fixture");
        File.WriteAllText(path, JsonSerializer.Serialize(new[] { original }));
        var store = new MetadataStore(path);
        var dialogs = new TestDialogs();
        using var viewModel = new MainViewModel(store, credentialStore, dialogs, new TestClipboard());
        viewModel.InitializeCommand.Execute(null);
        PumpUntil(() => viewModel.AddCommand.CanExecute(null));
        Assert(viewModel.Entries.Count == 1, "Initial list loads.");

        var external = Entry("External fixture");
        Complete(store.MutateAsync(transaction => transaction.Entries.Add(external)));
        var added = Entry("GUI fixture");
        createdIds.Add(added.Id);
        dialogs.NextCredential = new NewCredential(added, "non-secret-ui-test-fixture");
        viewModel.AddCommand.Execute(null);
        PumpUntil(() => viewModel.AddCommand.CanExecute(null));
        Assert(dialogs.ErrorCount == 0, "GUI registration succeeds.");
        Assert(viewModel.Entries.Count == 3, "GUI registration preserves an external addition.");
        Assert(Complete(store.LoadAsync()).Any(entry => entry.Id == external.Id), "External metadata survives GUI save.");

        Complete(store.MutateAsync(transaction => transaction.Entries.RemoveAll(entry => entry.Id == external.Id)));
        var second = Entry("Second GUI fixture");
        createdIds.Add(second.Id);
        dialogs.NextCredential = new NewCredential(second, "non-secret-ui-test-fixture");
        viewModel.AddCommand.Execute(null);
        PumpUntil(() => viewModel.AddCommand.CanExecute(null));
        Assert(dialogs.ErrorCount == 0, "A second GUI registration succeeds.");
        Assert(viewModel.Entries.All(entry => entry.Id != external.Id), "GUI save does not resurrect an externally deleted entry.");

        viewModel.SearchText = "missing-search-fixture";
        Assert(viewModel.IsEmpty, "Search handles no matching rows.");
        viewModel.SearchText = string.Empty;
        Assert(!viewModel.IsEmpty, "Clearing search restores the list.");
    }

    private static void RenderDemo(string directory, string output)
    {
        var entries = new[]
        {
            Entry("OpenAI API Key", "OpenAI", "Development", "로컬 개발용 · 데모 데이터", DateOnly.FromDateTime(DateTime.Today.AddDays(30))),
            Entry("GitHub Token", "GitHub", "Development", "저장소 자동화 · 데모 데이터", DateOnly.FromDateTime(DateTime.Today.AddDays(5))),
            Entry("Supabase API Key", "Supabase", "Staging", "스테이징 프로젝트 · 데모 데이터", null)
        };
        var path = Path.Combine(directory, "demo.json");
        File.WriteAllText(path, JsonSerializer.Serialize(entries));
        var dialogs = new TestDialogs();
        using var viewModel = new MainViewModel(new MetadataStore(path), new WindowsCredentialStore(), dialogs, new TestClipboard());
        viewModel.InitializeCommand.Execute(null);
        PumpUntil(() => viewModel.AddCommand.CanExecute(null));
        viewModel.SelectedEntry = viewModel.Entries[0];
        var window = new MainWindow
        {
            DataContext = viewModel,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Width = 1040,
            Height = 640
        };
        window.Show();
        var content = (FrameworkElement)window.Content;
        content.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1560, 960, 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using (var stream = File.Create(output))
        {
            encoder.Save(stream);
        }
        window.Close();
        Assert(File.Exists(output), "The actual application XAML renders to an image.");
        Console.WriteLine("Demo image rendered from production XAML with fixture metadata only.");
    }

    private static CredentialEntry Entry(string name, string service = "KeyMan tests", string environment = "Test", string notes = "", DateOnly? expires = null) => new()
    {
        Id = Guid.NewGuid(), Name = name, Service = service, Environment = environment,
        Notes = notes, ExpiresOn = expires, CreatedAt = DateTimeOffset.Now
    };

    private static T Complete<T>(Task<T> task)
    {
        PumpUntil(() => task.IsCompleted);
        return task.GetAwaiter().GetResult();
    }

    private static void PumpUntil(Func<bool> complete)
    {
        if (complete()) return;
        var frame = new DispatcherFrame();
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            if (complete() || DateTime.UtcNow - started > TimeSpan.FromSeconds(15))
            {
                frame.Continue = false;
            }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        if (!complete()) throw new TimeoutException("GUI fixture operation did not complete.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }
}

internal sealed class TestDialogs : IDialogService
{
    public int ErrorCount { get; private set; }
    public int AddCalls { get; private set; }
    public NewCredential? NextCredential { get; set; }
    public NewCredential? ShowAddCredential() { AddCalls++; return NextCredential; }
    public bool ConfirmDelete(string name) => true;
    public void ShowError(string message, Exception exception) => ErrorCount++;
}

internal sealed class TestClipboard : IClipboardService
{
    public Task<bool> CopyWithAutoClearAsync(string value, TimeSpan clearAfter) => Task.FromResult(true);
    public void Dispose() { }
}
