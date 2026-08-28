using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using WinCopyQueue.Core;

namespace WinCopyQueue;

public sealed class ExplorerPasteHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkControl = 0x11;
    private const int VkV = 0x56;
    private const uint CfHDrop = 15;

    private readonly Func<string, TransferOperation, IReadOnlyList<string>, Task> _pasteHandler;
    private readonly HookProcedure _hookProcedure;
    private IntPtr _hook;
    private bool _pasteKeyDown;

    public ExplorerPasteHook(Func<string, TransferOperation, IReadOnlyList<string>, Task> pasteHandler)
    {
        _pasteHandler = pasteHandler;
        _hookProcedure = HookCallback;
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = GetModuleHandle(module?.ModuleName);
        _hook = SetWindowsHookEx(WhKeyboardLl, _hookProcedure, moduleHandle, 0);
        if (_hook == IntPtr.Zero)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
    {
        if (code < 0)
        {
            return CallNextHookEx(_hook, code, message, data);
        }

        var keyboard = Marshal.PtrToStructure<KeyboardData>(data);
        var isKeyDown = message == (IntPtr)WmKeyDown || message == (IntPtr)WmSysKeyDown;
        var isKeyUp = message == (IntPtr)WmKeyUp || message == (IntPtr)WmSysKeyUp;

        if (keyboard.VirtualKey == VkV && isKeyUp)
        {
            _pasteKeyDown = false;
        }

        if (keyboard.VirtualKey != VkV || !isKeyDown || _pasteKeyDown ||
            (GetAsyncKeyState(VkControl) & 0x8000) == 0)
        {
            return CallNextHookEx(_hook, code, message, data);
        }

        var foreground = GetForegroundWindow();
        if (!IsExplorerWindow(foreground))
        {
            AppLog.Write($"Ctrl+V pominięte: aktywne okno 0x{foreground.ToInt64():X} nie należy do Explorera.");
            return CallNextHookEx(_hook, code, message, data);
        }

        if (!IsClipboardFormatAvailable(CfHDrop))
        {
            AppLog.Write("Ctrl+V pominięte: schowek nie zawiera formatu CF_HDROP.");
            return CallNextHookEx(_hook, code, message, data);
        }

        _pasteKeyDown = true;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            async () => await CompletePasteAsync(foreground),
            System.Windows.Threading.DispatcherPriority.Background);

        // Zwrócenie wartości różnej od zera blokuje natywne, równoległe kopiowanie Explorera.
        return (IntPtr)1;
    }

    private async Task CompletePasteAsync(IntPtr explorerWindow)
    {
        if (!ExplorerDestinationResolver.TryGetFolder(explorerWindow, out var destination))
        {
            AppLog.Write($"Ctrl+V przejęte, ale nie rozpoznano folderu dla okna 0x{explorerWindow.ToInt64():X}.");
            return;
        }

        if (!ClipboardTransferReader.TryRead(out var operation, out var sources))
        {
            AppLog.Write("Ctrl+V przejęte, ale schowek nie zawiera już istniejących plików.");
            return;
        }

        AppLog.Write($"Ctrl+V przejęte: {operation}, źródeł: {sources.Count}, cel: {destination}.");
        await _pasteHandler(destination, operation, sources);
    }

    private static bool IsExplorerWindow(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(window, out var processId);
        try
        {
            using var process = Process.GetProcessById(unchecked((int)processId));
            return process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private delegate IntPtr HookProcedure(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardData
    {
        public int VirtualKey;
        public int ScanCode;
        public int Flags;
        public int Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, HookProcedure callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}

internal static class ExplorerDestinationResolver
{
    private const uint GaRoot = 2;

    public static bool TryGetFolder(IntPtr explorerWindow, out string destination)
    {
        destination = string.Empty;
        object? shell = null;
        object? windows = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
            {
                return false;
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return false;
            }

            dynamic dynamicShell = shell;
            windows = dynamicShell.Windows();
            if (windows is null)
            {
                return false;
            }

            dynamic dynamicWindows = windows;
            var count = Convert.ToInt32(dynamicWindows.Count);
            var targetRoot = GetAncestor(explorerWindow, GaRoot);
            AppLog.Write($"Resolver Explorera: okien ShellWindows={count}, aktywne=0x{explorerWindow.ToInt64():X}, root=0x{targetRoot.ToInt64():X}.");
            for (var index = 0; index < count; index++)
            {
                object? window = dynamicWindows.Item(index);
                if (window is null)
                {
                    continue;
                }

                try
                {
                    dynamic dynamicWindow = window;
                    var handle = new IntPtr(Convert.ToInt64(dynamicWindow.HWND));
                    var candidateRoot = GetAncestor(handle, GaRoot);
                    AppLog.Write($"Resolver Explorera: kandydat {index}, hwnd=0x{handle.ToInt64():X}, root=0x{candidateRoot.ToInt64():X}.");
                    if (handle != explorerWindow && candidateRoot != targetRoot)
                    {
                        continue;
                    }

                    string? path = dynamicWindow.Document?.Folder?.Self?.Path as string;
                    if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    {
                        destination = Path.GetFullPath(path);
                        return true;
                    }
                }
                finally
                {
                    if (Marshal.IsComObject(window))
                    {
                        Marshal.ReleaseComObject(window);
                    }
                }
            }

            return IsDesktopWindow(explorerWindow) && TryGetDesktop(out destination);
        }
        catch (Exception exception)
        {
            AppLog.Write("Resolver Explorera zgłosił wyjątek.", exception);
            return false;
        }
        finally
        {
            if (windows is not null && Marshal.IsComObject(windows))
            {
                Marshal.ReleaseComObject(windows);
            }
            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.ReleaseComObject(shell);
            }
        }
    }

    private static bool IsDesktopWindow(IntPtr window)
    {
        var className = new StringBuilder(64);
        GetClassName(window, className, className.Capacity);
        return className.ToString() is "Progman" or "WorkerW";
    }

    private static bool TryGetDesktop(out string desktop)
    {
        desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Directory.Exists(desktop);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);
}
