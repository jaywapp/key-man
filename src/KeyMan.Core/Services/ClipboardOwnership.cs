using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyMan.Services;

public static class ClipboardOwnership
{
    public static uint WriteText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var window = CreateWindowEx(0, "STATIC", "KeyMan Clipboard", 0, 0, 0, 0, 0,
            new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (window == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var memory = IntPtr.Zero;
        var opened = false;
        try
        {
            var byteCount = checked((value.Length + 1) * sizeof(char));
            memory = GlobalAlloc(0x0002, (nuint)byteCount);
            if (memory == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var pointer = GlobalLock(memory);
            if (pointer == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                Marshal.Copy(value.ToCharArray(), 0, pointer, value.Length);
                Marshal.WriteInt16(pointer, value.Length * sizeof(char), 0);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            OpenWithRetry(window);
            opened = true;
            if (!EmptyClipboard() || SetClipboardData(13, memory) == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            memory = IntPtr.Zero;
            return GetClipboardSequenceNumber();
        }
        finally
        {
            if (opened) CloseClipboard();
            if (memory != IntPtr.Zero) GlobalFree(memory);
            DestroyWindow(window);
        }
    }

    public static bool ClearIfOwned(uint sequence)
    {
        OpenWithRetry(IntPtr.Zero);
        try
        {
            if (GetClipboardSequenceNumber() != sequence)
            {
                return false;
            }

            if (!EmptyClipboard())
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void OpenWithRetry(IntPtr window)
    {
        var error = 0;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(window)) return;
            error = Marshal.GetLastWin32Error();
            Thread.Sleep(20);
        }
        throw new Win32Exception(error, "The clipboard is busy. Clear the copied value manually.");
    }

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
}
