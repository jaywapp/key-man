using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using KeyMan.Models;

internal static class Program
{
    private const int FramesPerSecond = 4;
    private static int _frameNumber;
    private static string _frames = "";
    private static readonly List<object> Scenes = [];
    private static readonly Typeface Face = new(new FontFamily("Segoe UI, Malgun Gothic"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Usage: KeyMan.Demo <repository> <frame-directory> <media-directory>");
        var root = Path.GetFullPath(args[0]);
        _frames = Path.GetFullPath(args[1]);
        var media = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(_frames);
        Directory.CreateDirectory(media);
        var fixtureDirectory = Path.Combine(_frames, "fixture");
        Directory.CreateDirectory(fixtureDirectory);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        LoadResources(app, Path.Combine(root, "src", "KeyMan", "App.xaml"));
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var showingAdd = false;
        var model = new DemoModel(() => showingAdd = true);
        model.Register(Entry("71d39f9d-e923-4ff2-8b3c-167876f36601", "GitHub Token", "GitHub", "Development", "저장소 자동화 · 가짜 데이터"));
        model.Register(Entry("71d39f9d-e923-4ff2-8b3c-167876f36602", "Supabase API Key", "Supabase", "Staging", "스테이징 프로젝트 · 가짜 데이터"));
        model.StatusText = "2개 항목 · 가짜 데이터";
        var main = LoadWindow(Path.Combine(root, "src", "KeyMan", "MainWindow.xaml"), model, 1040, 680);
        var form = new DemoForm(entry => { model.Register(entry); showingAdd = false; });
        var add = LoadWindow(Path.Combine(root, "src", "KeyMan", "AddCredentialWindow.xaml"), form, 500, 660);
        Find<PasswordBox>(add, "SecretPasswordBox").PasswordChanged += (_, _) => form.Secret = Find<PasswordBox>(add, "SecretPasswordBox").Password;
        try
        {
            Scene(main, null, "등록 → 검색 → 복사", "Windows 로컬 API 키 보관함의 사용 흐름", 3, "AddButton");
            Execute(main, "AddButton");
            if (!showingAdd) throw new InvalidOperationException("The production add binding did not execute.");
            Scene(main, add, "1. 키 등록", "입력 화면도 현재 앱의 XAML을 사용합니다.", 1.5, "NameTextBox");
            Find<TextBox>(add, "NameTextBox").SetCurrentValue(TextBox.TextProperty, "OpenAI API Key");
            Scene(main, add, "1. 키 등록", "이름·서비스·환경으로 키를 구분합니다.", 1.5, "ServiceTextBox");
            Find<TextBox>(add, "ServiceTextBox").SetCurrentValue(TextBox.TextProperty, "OpenAI");
            Find<ComboBox>(add, "EnvironmentComboBox").SetCurrentValue(ComboBox.TextProperty, "Development");
            Find<PasswordBox>(add, "SecretPasswordBox").Password = "demo-value-not-a-real-key";
            Find<TextBox>(add, "NotesTextBox").SetCurrentValue(TextBox.TextProperty, "로컬 개발용 · 가짜 데이터");
            Scene(main, add, "1. 키 등록", "가짜 입력값으로 등록 화면을 재현합니다.", 3, "SaveButton");
            Execute(add, "SaveButton");
            if (showingAdd || model.Entries.Count != 3) throw new InvalidOperationException("Registration fixture failed.");
            Scene(main, null, "등록한 키가 목록에", "등록은 메모리에서만 재현합니다. 실제 보관함은 사용하지 않습니다.", 3);
            foreach (var (query, seconds) in new[] { ("O", .5), ("Open", 1.0), ("OpenAI", 1.5) })
            {
                Find<TextBox>(main, "SearchTextBox").SetCurrentValue(TextBox.TextProperty, query);
                Scene(main, null, "2. 키 검색", "이름·서비스·환경으로 필요한 항목을 찾습니다.", seconds, "SearchTextBox");
            }
            if (Find<DataGrid>(main, "KeyList").Items.Count != 1) throw new InvalidOperationException("Search fixture failed.");
            Find<DataGrid>(main, "KeyList").SelectedIndex = 0;
            Scene(main, null, "3. 값 복사", "항목을 선택한 뒤 값 복사를 누릅니다.", 3, "CopyButton");
            Execute(main, "CopyButton");
            if (model.CopyCount != 1) throw new InvalidOperationException("Copy fixture did not execute.");
            Scene(main, null, "3. 값 복사", "데모: 실제 클립보드 미사용 · 30초 정리는 시연하지 않습니다.", 4, "StatusLabel");
            File.Copy(Path.Combine(_frames, $"frame-{_frameNumber:0000}.png"), Path.Combine(media, "keyman-demo-poster.png"), true);
            var entries = model.Entries.ToArray();
            var evidence = Task.Run(() => FixtureEvidence.CollectAsync(root, fixtureDirectory, entries)).GetAwaiter().GetResult();
            Scene(main, null, "CLI·MCP에서도 같은 메타데이터", "별도 fixture JSON에 대한 실제 목록 응답 예시입니다.", 4, evidence: evidence);
            var sourceFiles = new[] { "App.xaml", "MainWindow.xaml", "AddCredentialWindow.xaml" };
            var sourceHashes = sourceFiles.Select(file => new
            {
                path = "src/KeyMan/" + file,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "src", "KeyMan", file)))).ToLowerInvariant()
            }).ToArray();
            File.WriteAllText(Path.Combine(media, "fixture-evidence.json"), JsonSerializer.Serialize(new
            {
                method = "Production XAML rendering with in-memory GUI fixtures; not a desktop recording.",
                gui = "Registration and copy are simulated. No Windows credentials or system clipboard are accessed.",
                cliCommand = "key-man.exe list OpenAI --json", cliResponse = JsonDocument.Parse(evidence.Cli).RootElement,
                mcpTool = "keyman_list", mcpArguments = new { query = "OpenAI" }, mcpTextResponse = evidence.Mcp,
                sourceHashes, frames = _frameNumber, framesPerSecond = FramesPerSecond,
                durationSeconds = _frameNumber / (double)FramesPerSecond, scenes = Scenes
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS: {_frameNumber} rendered frames; GUI register/search/copy fixtures; read-only CLI/MCP responses.");
            return 0;
        }
        finally { add.Close(); main.Close(); app.Shutdown(); }
    }

    private static CredentialEntry Entry(string id, string name, string service, string environment, string notes) => new()
    {
        Id = Guid.Parse(id), Name = name, Service = service, Environment = environment, Notes = notes,
        CreatedAt = new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.Zero)
    };

    private static void LoadResources(Application app, string path)
    {
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resourceNode = XDocument.Load(path).Root!.Element(wpf + "Application.Resources")!;
        var dictionary = new XElement(wpf + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"), resourceNode.Elements());
        app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
    }

    private static Window LoadWindow(string path, object context, int width, int height)
    {
        var document = XDocument.Load(path);
        document.Root!.Attribute(XName.Get("Class", "http://schemas.microsoft.com/winfx/2006/xaml"))!.Remove();
        foreach (var node in document.Descendants()) node.Attribute("PasswordChanged")?.Remove();
        var window = (Window)XamlReader.Parse(document.ToString());
        window.DataContext = context;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10000; window.Top = -10000;
        window.ShowActivated = false; window.ShowInTaskbar = false;
        window.WindowStyle = WindowStyle.None; window.ResizeMode = ResizeMode.NoResize;
        window.Width = width; window.Height = height;
        window.Show();
        Settle(window);
        return window;
    }

    private static T Find<T>(Window window, string name) where T : FrameworkElement =>
        window.FindName(name) as T ?? throw new InvalidOperationException($"Missing production element: {name}");
    private static void Execute(Window window, string name)
    {
        var button = Find<Button>(window, name);
        if (button.Command?.CanExecute(null) != true) throw new InvalidOperationException($"Disabled fixture action: {name}");
        button.Command.Execute(null);
    }
    private static void Settle(Window window)
    {
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static BitmapSource Capture(Window window)
    {
        Settle(window);
        var bitmap = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        return bitmap;
    }
    private static void Text(DrawingContext context, string value, double x, double y, double size, string color, double width = 1000)
    {
        var text = new FormattedText(value, CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight,
            Face, size, (Brush)new BrushConverter().ConvertFromString(color)!, 1) { MaxTextWidth = width };
        context.DrawText(text, new Point(x, y));
    }
    private static void Scene(Window main, Window? add, string title, string caption, double seconds,
        string? highlight = null, FixtureEvidence? evidence = null)
    {
        var start = _frameNumber / (double)FramesPerSecond;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1080, 1080));
            Text(context, "KeyMan", 28, 28, 38, "#172334");
            Text(context, "가짜 데이터 · 화면 재현", 718, 45, 20, "#56657A", 340);
            Text(context, title, 28, 98, 32, "#172334");
            Text(context, caption, 28, 146, 20, "#43526A");
            var mainImage = Capture(main);
            context.DrawImage(mainImage, new Rect(20, 218, 1040, 680));
            var shown = main;
            var offset = new Point(20, 218);
            if (add is not null)
            {
                context.DrawRectangle(new SolidColorBrush(Color.FromArgb(195, 243, 245, 248)), null, new Rect(20, 218, 1040, 680));
                context.DrawImage(Capture(add), new Rect(290, 218, 500, 660));
                shown = add; offset = new Point(290, 218);
            }
            if (highlight is not null)
            {
                var target = Find<FrameworkElement>(shown, highlight);
                var bounds = target.TransformToAncestor(shown).TransformBounds(new Rect(target.RenderSize));
                bounds.Offset(offset.X, offset.Y); bounds.Inflate(4, 4);
                context.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(36, 95, 193)), 3), bounds, 6, 6);
            }
            if (evidence is not null)
            {
                context.DrawRectangle(new SolidColorBrush(Color.FromRgb(243, 245, 248)), null, new Rect(20, 218, 1040, 680));
                Text(context, "CLI 목록 응답 예시", 52, 252, 26, "#172334");
                Text(context, "key-man.exe list OpenAI --json", 52, 300, 22, "#245FC1");
                Text(context, evidence.Cli, 52, 350, 21, "#25364B", 970);
                Text(context, "MCP 목록 응답 예시", 52, 510, 26, "#172334");
                Text(context, "keyman_list(query: \"OpenAI\")", 52, 558, 22, "#245FC1");
                Text(context, evidence.Mcp, 52, 608, 21, "#25364B", 970);
                Text(context, "실제 fixture 응답 · 이름·서비스·환경·만료일만 포함", 52, 820, 20, "#56657A");
            }
            Text(context, "운영 XAML + 인메모리 GUI 재현", 28, 940, 20, "#43526A");
            Text(context, "실제 키·보관함·시스템 클립보드 미사용", 28, 976, 20, "#56657A");
            Text(context, "등록·검색·복사 흐름 / CLI·MCP는 별도 가짜 메타데이터의 실제 목록 응답", 28, 1020, 16, "#56657A");
        }
        var bitmap = new RenderTargetBitmap(1080, 1080, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var count = (int)Math.Round(seconds * FramesPerSecond);
        var first = Path.Combine(_frames, $"frame-{++_frameNumber:0000}.png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var output = File.Create(first)) encoder.Save(output);
        for (var index = 1; index < count; index++)
            File.Copy(first, Path.Combine(_frames, $"frame-{++_frameNumber:0000}.png"), true);
        Scenes.Add(new { startSeconds = start, durationSeconds = seconds, title, caption, highlight });
    }
}
