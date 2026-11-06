using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KeyMan;
using KeyMan.Services;
using KeyMan.ViewModels;

internal static partial class Program
{
    private static void VerifyWindowBindings(string directory, string? screenshots)
    {
        var entries = new[]
        {
            Entry("OpenAI API Key", "OpenAI", "Development", "로컬 개발용 · 데모 데이터", DateOnly.FromDateTime(DateTime.Today.AddDays(30))),
            Entry("GitHub Token", "GitHub", "Development", "저장소 자동화 · 데모 데이터", DateOnly.FromDateTime(DateTime.Today.AddDays(5))),
            Entry("Supabase API Key", "Supabase", "Staging", "스테이징 프로젝트 · 데모 데이터")
        };
        var path = Path.Combine(directory, "window-bindings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(entries));
        using var model = new MainViewModel(new MetadataStore(path), new WindowsCredentialStore(), new TestDialogs(), new TestClipboard());
        model.InitializeCommand.Execute(null);
        PumpUntil(() => model.AddCommand.CanExecute(null));
        var main = new MainWindow { DataContext = model };
        PrepareWindow(main);
        try
        {
            main.Show();
            SettleLayout(main);
            var list = Find<DataGrid>(main, "KeyList");
            var search = Find<TextBox>(main, "SearchTextBox");
            var copy = Find<Button>(main, "CopyButton");
            var delete = Find<Button>(main, "DeleteButton");
            Assert(list.Items.Count == 3, "Production grid binds the metadata list.");
            Assert(ReferenceEquals(Find<Button>(main, "AddButton").Command, model.AddCommand), "Production add button retains its command.");
            Assert(ReferenceEquals(copy.Command, model.CopyCommand) && ReferenceEquals(delete.Command, model.DeleteCommand), "Copy and delete keep their original commands.");
            Assert(!copy.IsEnabled && !delete.IsEnabled, "Selection actions are disabled without a selection.");
            list.SelectedIndex = 0;
            SettleLayout(main);
            Assert(model.SelectedEntry?.Name == entries[0].Name && copy.IsEnabled && delete.IsEnabled, "Selecting a real grid row enables the original actions.");
            Capture(main, screenshots, "main", 1040, 680);
            Capture(main, screenshots, "main-compact", 820, 520);

            search.SetCurrentValue(TextBox.TextProperty, "GitHub");
            SettleLayout(main);
            Assert(model.SearchText == "GitHub" && list.Items.Count == 1, "Search text updates the view model and filters the real grid.");
            search.SetCurrentValue(TextBox.TextProperty, "no-matching-key");
            SettleLayout(main);
            Assert(model.IsEmpty && list.Items.Count == 0, "No-result search updates the empty view.");
            Assert(!copy.IsEnabled && !delete.IsEnabled, "Filtering out a selection disables copy and delete.");
            Capture(main, screenshots, "search-empty", 820, 520);
            search.SetCurrentValue(TextBox.TextProperty, string.Empty);
            SettleLayout(main);
            Assert(list.Items.Count == 3, "Clearing the real search input restores all rows.");

            model.Entries[0] = Entry(
                "Development Shared Integration Credential With A Long Name",
                "Integration Service With A Long Name", "Development-Integration",
                "긴 메모의 전체 내용은 툴팁으로 확인합니다. 실제 키가 없는 데모 데이터입니다.");
            list.SelectedIndex = 0;
            Assert(ReferenceEquals(list.Items[0], model.Entries[0]) && model.Entries[0].Name.Length > 50, "The long-content fixture is displayed by the actual grid.");
            Capture(main, screenshots, "long-content", 820, 520);
            model.Entries.Clear();
            search.SetCurrentValue(TextBox.TextProperty, " ");
            search.SetCurrentValue(TextBox.TextProperty, string.Empty);
            Capture(main, screenshots, "vault-empty", 820, 520);
        }
        finally
        {
            main.Close();
        }

        var addModel = new AddCredentialViewModel();
        var add = new AddCredentialWindow(addModel);
        PrepareWindow(add);
        try
        {
            add.Show();
            SettleLayout(add);
            var name = Find<TextBox>(add, "NameTextBox");
            var service = Find<TextBox>(add, "ServiceTextBox");
            var environment = Find<ComboBox>(add, "EnvironmentComboBox");
            var expiration = Find<DatePicker>(add, "ExpirationDatePicker");
            var notes = Find<TextBox>(add, "NotesTextBox");
            var secret = Find<PasswordBox>(add, "SecretPasswordBox");
            var save = Find<Button>(add, "SaveButton");
            Assert(ReferenceEquals(save.Command, addModel.SaveCommand), "Save retains the validation command.");
            save.Command.Execute(null);
            SettleLayout(add);
            Assert(Find<TextBlock>(add, "ErrorTextBlock").Text == "이름을 입력해 주세요.", "Required-name validation is visible in the production form.");
            AssertValidationVisible(add);
            Capture(add, screenshots, "add-error", 500, 660);

            name.SetCurrentValue(TextBox.TextProperty, "OpenAI API Key");
            service.SetCurrentValue(TextBox.TextProperty, "OpenAI");
            environment.SetCurrentValue(ComboBox.TextProperty, "Staging");
            expiration.SetCurrentValue(DatePicker.SelectedDateProperty, DateTime.Today.AddDays(30));
            notes.SetCurrentValue(TextBox.TextProperty, "로컬 개발용 · 데모 데이터");
            SettleLayout(add);
            Assert(addModel.Name == "OpenAI API Key" && addModel.Service == "OpenAI" && addModel.Environment == "Staging", "Text and environment fields update the model.");
            Assert(addModel.ExpirationDate == DateTime.Today.AddDays(30) && addModel.Notes.Contains("데모"), "Expiration and notes keep their bindings.");
            save.Command.Execute(null);
            SettleLayout(add);
            Assert(Find<TextBlock>(add, "ErrorTextBlock").Text == "키 또는 토큰을 입력해 주세요.", "Required-secret validation remains visible.");
            secret.Password = "fake-ui-fixture-not-a-key";
            Assert(addModel.Secret == "fake-ui-fixture-not-a-key", "PasswordChanged still updates the registration model.");
            secret.Clear();
            Assert(addModel.Secret.Length == 0, "Clearing the password removes the model value.");
            Capture(add, screenshots, "add", 500, 660);
            Capture(add, screenshots, "add-compact", 500, 520);
            add.Height = 520;
            SettleLayout(add);
            AssertValidationVisible(add);
        }
        finally
        {
            add.Close();
        }
    }

    private static T Find<T>(Window window, string name) where T : FrameworkElement =>
        window.FindName(name) as T ?? throw new InvalidOperationException($"Missing production control: {name}.");

    private static void AssertValidationVisible(Window window)
    {
        var error = Find<TextBlock>(window, "ErrorTextBlock");
        var save = Find<Button>(window, "SaveButton");
        var errorBounds = error.TransformToAncestor(window).TransformBounds(new Rect(error.RenderSize));
        var saveBounds = save.TransformToAncestor(window).TransformBounds(new Rect(save.RenderSize));
        Assert(error.IsVisible && errorBounds.Height > 0 && errorBounds.Top >= 0 &&
               errorBounds.Bottom <= saveBounds.Top && saveBounds.Bottom <= window.ActualHeight,
            "Validation remains visible above the save action at the current window height.");
    }

    private static void PrepareWindow(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10000;
        window.Top = -10000;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
    }

    private static void SettleLayout(Window window)
    {
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void Capture(Window window, string? directory, string name, int width, int height)
    {
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        window.Width = width;
        window.Height = height;
        SettleLayout(window);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(stream);
    }
}
