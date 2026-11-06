using System.ComponentModel;
using System.Runtime.InteropServices;
using KeyMan.Services;

internal static class NativeClipboardTests
{
    public static void Run()
    {
        var originalStation = GetProcessWindowStation();
        var originalDesktop = GetThreadDesktop(GetCurrentThreadId());
        var station = CreateWindowStation("KeyManTests-" + Guid.NewGuid().ToString("N"), 1, 0x037F, IntPtr.Zero);
        if (station == IntPtr.Zero && Marshal.GetLastWin32Error() == 5)
        {
            if (Environment.GetEnvironmentVariable("KEYMAN_REQUIRE_NATIVE_CLIPBOARD_TESTS") == "1")
            {
                throw new Win32Exception(5, "Native clipboard verification requires a private window station; publication must wait.");
            }
            Console.WriteLine("SKIP: native clipboard checks require permission to create a private window station; user clipboard was not accessed.");
            return;
        }
        Require(station != IntPtr.Zero, "Create isolated window station");
        var desktop = IntPtr.Zero;
        var stationChanged = false;
        var desktopChanged = false;
        try
        {
            Require(SetProcessWindowStation(station), "Select isolated window station");
            stationChanged = true;
            desktop = CreateDesktop("KeyManTestDesktop", IntPtr.Zero, IntPtr.Zero, 0, 0x01FF, IntPtr.Zero);
            Require(desktop != IntPtr.Zero, "Create isolated desktop");
            Require(SetThreadDesktop(desktop), "Select isolated desktop");
            desktopChanged = true;

            var first = ClipboardOwnership.WriteText("native-fixture-한글");
            Assert(first != 0 && !ClipboardOwnership.ClearIfOwned(0), "Unclaimed ownership cannot clear a native copy.");
            Assert(ReadFixture() == "native-fixture-한글", "Native write preserves Unicode text.");
            var second = ClipboardOwnership.WriteText("native-fixture-한글");
            Assert(first != second, "A new native copy advances ownership.");
            Assert(!ClipboardOwnership.ClearIfOwned(first), "Old owner cannot clear a new copy.");
            Assert(ReadFixture() == "native-fixture-한글", "Intervening copy remains intact.");
            Assert(ClipboardOwnership.ClearIfOwned(second), "Current owner can clear its copy.");
            Assert(ReadFixture() is null, "Owned native clipboard is empty after clearing.");

            using var lifetime = new ClipboardAutoClear(ClipboardOwnership.WriteText, ClipboardOwnership.ClearIfOwned);
            Assert(lifetime.CopyAsync("expiry-fixture", TimeSpan.FromMilliseconds(30)).GetAwaiter().GetResult(),
                "Native timer clears its owned copy.");
            var pending = lifetime.CopyAsync("shutdown-fixture", TimeSpan.FromMinutes(1));
            lifetime.Dispose();
            Assert(ReadFixture() is null, "Normal disposal clears the native clipboard.");
            try { pending.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
            Console.WriteLine("PASS: 9 native clipboard checks on a private window station (user clipboard untouched)");
        }
        finally
        {
            if (stationChanged) Require(SetProcessWindowStation(originalStation), "Restore test process station");
            if (desktopChanged) Require(SetThreadDesktop(originalDesktop), "Restore test thread desktop");
            if (desktop != IntPtr.Zero) CloseDesktop(desktop);
            CloseWindowStation(station);
        }
    }

    private static string? ReadFixture()
    {
        Require(OpenClipboard(IntPtr.Zero), "Read isolated clipboard");
        try
        {
            var memory = GetClipboardData(13);
            if (memory == IntPtr.Zero) return null;
            var pointer = GlobalLock(memory);
            Require(pointer != IntPtr.Zero, "Read fixture memory");
            try { return Marshal.PtrToStringUni(pointer); }
            finally { GlobalUnlock(memory); }
        }
        finally { CloseClipboard(); }
    }

    private static void Require(bool success, string operation)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), operation);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [DllImport("user32.dll", EntryPoint = "CreateWindowStationW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowStation(string? name, uint flags, uint access, IntPtr attributes);
    [DllImport("user32.dll")]
    private static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWindowStation(IntPtr station);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseWindowStation(IntPtr station);
    [DllImport("user32.dll", EntryPoint = "CreateDesktopW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr attributes);
    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDesktop(uint threadId);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetThreadDesktop(IntPtr desktop);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);
}
