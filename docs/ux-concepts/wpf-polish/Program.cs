using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            if (args.Length == 2 && args[0] == "--render")
            {
                RenderAll(Path.GetFullPath(args[1]));
                app.Shutdown();
                return 0;
            }

            var concept = args.Length > 0 ? args[0] : "concept-01";
            var window = CreateMain(concept);
            window.Closed += (_, _) => app.Shutdown();
            app.Run(window);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static Window Load(string concept, string name)
    {
        if (concept is not ("concept-01" or "concept-02" or "concept-03"))
            throw new ArgumentException("Unknown concept.");
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, concept, name + ".xaml"));
        return (Window)XamlReader.Load(stream);
    }

    private static T Element<T>(Window window, string name) where T : FrameworkElement =>
        window.FindName(name) as T ?? throw new InvalidOperationException($"Missing {name} in {window.Title}.");

    private static Window CreateMain(string concept)
    {
        var window = Load(concept, "Main");
        var entries = new ObservableCollection<DemoEntry>
        {
            new("OpenAI API Key", "OpenAI", "Development", "30일 후 만료", "로컬 개발용 · 데모 데이터"),
            new("GitHub Token", "GitHub", "Development", "5일 후 만료", "저장소 자동화 · 데모 데이터"),
            new("Supabase API Key", "Supabase", "Staging", "만료일 없음", "스테이징 프로젝트 · 데모 데이터")
        };
        var search = Element<TextBox>(window, "SearchBox");
        var list = Element<Selector>(window, "KeyList");
        var copy = Element<Button>(window, "CopyButton");
        var delete = Element<Button>(window, "DeleteButton");
        var status = Element<TextBlock>(window, "StatusLabel");
        var view = CollectionViewSource.GetDefaultView(entries);
        list.ItemsSource = view;
        list.SelectedIndex = 0;
        status.Text = "디자인 미리보기 · 가짜 데이터 3개";
        search.TextChanged += (_, _) =>
        {
            view.Filter = item => item is DemoEntry entry &&
                string.Join(" ", entry.Name, entry.Service, entry.Environment).Contains(search.Text, StringComparison.OrdinalIgnoreCase);
            status.Text = view.IsEmpty ? "검색 결과가 없습니다. 다른 검색어를 입력하세요." : "디자인 미리보기 · 가짜 데이터만 표시합니다.";
        };
        list.SelectionChanged += (_, _) => copy.IsEnabled = delete.IsEnabled = list.SelectedItem is DemoEntry;
        copy.Click += (_, _) => status.Text = "복사 완료 상태 예시 · 실제 클립보드는 변경하지 않습니다.";
        delete.Click += (_, _) =>
        {
            if (list.SelectedItem is DemoEntry entry) entries.Remove(entry);
            status.Text = "미리보기의 가짜 항목을 삭제했습니다.";
        };
        Element<Button>(window, "AddButton").Click += (_, _) =>
        {
            var dialog = CreateAdd(concept, entries);
            dialog.Owner = window;
            dialog.ShowDialog();
        };
        return window;
    }

    private static Window CreateAdd(string concept, ObservableCollection<DemoEntry> entries)
    {
        var window = Load(concept, "Add");
        var name = Element<TextBox>(window, "NameField");
        var service = Element<TextBox>(window, "ServiceField");
        var environment = Element<ComboBox>(window, "EnvironmentField");
        environment.Items.Clear();
        environment.ItemsSource = new[] { "Development", "Staging", "Production" };
        environment.Text = "Development";
        var secret = Element<PasswordBox>(window, "SecretField");
        var expiration = Element<DatePicker>(window, "ExpirationField");
        var notes = Element<TextBox>(window, "NotesField");
        var error = Element<TextBlock>(window, "ErrorLabel");
        Element<Button>(window, "CancelButton").Click += (_, _) => window.Close();
        Element<Button>(window, "SaveButton").Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text))
            {
                error.Text = "이름을 입력해 주세요.";
                name.Focus();
                return;
            }
            entries.Add(new(name.Text, service.Text, environment.Text,
                expiration.SelectedDate?.ToString("yyyy-MM-dd") ?? "만료일 없음", notes.Text));
            secret.Clear();
            window.Close();
        };
        return window;
    }

    private static void RenderAll(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var concept in new[] { "concept-01", "concept-02", "concept-03" })
        {
            var main = CreateMain(concept);
            Prepare(main);
            main.Show();
            Render(main, Path.Combine(output, concept + ".png"), 1040, 680);
            Render(main, Path.Combine(output, concept + "-compact.png"), 820, 520);
            var search = Element<TextBox>(main, "SearchBox");
            var list = Element<Selector>(main, "KeyList");
            search.Text = "GitHub";
            if (list.Items.Count != 1) throw new InvalidOperationException("Preview search failed.");
            search.Text = "missing";
            if (list.Items.Count != 0) throw new InvalidOperationException("Preview empty search failed.");
            search.Text = string.Empty;
            list.SelectedIndex = 0;
            Element<Button>(main, "CopyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Element<Button>(main, "DeleteButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (list.Items.Count != 2) throw new InvalidOperationException("Preview deletion failed.");
            list.ItemsSource = new[]
            {
                new DemoEntry("Development Shared Integration Credential With A Long Name", "Integration Service With A Long Name", "Development-Integration", "2026-12-06", "긴 메모가 목록 너비를 넘을 때 원문을 확인할 수 있어야 합니다. 가짜 데이터입니다.")
            };
            list.SelectedIndex = 0;
            Render(main, Path.Combine(output, concept + "-long.png"), 820, 520);
            main.Close();

            var entries = new ObservableCollection<DemoEntry>();
            var add = CreateAdd(concept, entries);
            Prepare(add);
            Element<TextBox>(add, "NameField").Text = "OpenAI API Key";
            Element<TextBox>(add, "ServiceField").Text = "OpenAI";
            Element<TextBox>(add, "NotesField").Text = "로컬 개발용 · 데모 데이터";
            add.Show();
            Render(add, Path.Combine(output, concept + "-add.png"), 500, 660);
            Element<Button>(add, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (entries.Count != 1) throw new InvalidOperationException("Preview addition failed.");
        }
        File.WriteAllText(Path.Combine(output, "verification.txt"), "PASS: 3 WPF concepts, 12 renders including long fixture content, fake-data search / empty / copy status / deletion / addition. No vault or clipboard access.\n");
    }

    private static void Prepare(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10000;
        window.Top = -10000;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
    }

    private static void Render(Window window, string path, int width, int height)
    {
        window.Width = width;
        window.Height = height;
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}

public sealed record DemoEntry(string Name, string Service, string Environment, string ExpirationStatus, string Notes);
