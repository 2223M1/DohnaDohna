using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

if (args.Length < 2 || !File.Exists(args[0]) || !int.TryParse(args[1], out int seconds) || seconds is < 1 or > 1800)
    throw new ArgumentException("Expected an isolated game executable and a timeout (1..1800 seconds).");
var desktop = Native.CreateDesktop("DohnaDohnaSmoke-" + Environment.ProcessId, 0, 0, 0, 0x000F01FF, 0);
if (desktop == 0) throw new Win32Exception();
var startup = new Native.StartupInfo
{
    cb = Marshal.SizeOf<Native.StartupInfo>(), lpDesktop = @"winsta0\DohnaDohnaSmoke-" + Environment.ProcessId,
    // Show the child on its own inactive desktop so the host's real focus/input
    // gates can run. This does not switch or activate the user's desktop.
    dwFlags = 0x101, wShowWindow = 5,
    hStdInput = Native.GetStdHandle(-10), hStdOutput = Native.GetStdHandle(-11), hStdError = Native.GetStdHandle(-12)
};
string Quote(string argument) => "\"" + argument.Replace("\"", "\\\"") + "\"";
var command = new StringBuilder(string.Join(" ", new[] { args[0] }.Concat(args.Skip(2)).Select(Quote)));
try
{
    if (!Native.CreateProcess(args[0], command, 0, 0, true, 0, 0, Path.GetDirectoryName(args[0]), ref startup, out var process))
        throw new Win32Exception();
    if (Environment.GetEnvironmentVariable("DOHNA_CAPTURE_DIR") is { Length: > 0 } audioDirectory)
        File.WriteAllText(Path.Combine(audioDirectory, "game.pid"), process.processId.ToString());
    Native.CloseHandle(process.hThread);
    try
    {
        if (Native.WaitForSingleObject(process.hProcess, (uint)(seconds * 1000)) == 258)
        {
            if (!Native.TerminateProcess(process.hProcess, 124)) throw new Win32Exception();
            Native.WaitForSingleObject(process.hProcess, 5000);
            return 124;
        }
        if (!Native.GetExitCodeProcess(process.hProcess, out uint exitCode)) throw new Win32Exception();
        return (int)exitCode;
    }
    finally { Native.CloseHandle(process.hProcess); }
}
finally { Native.CloseDesktop(desktop); }

static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct StartupInfo
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInfo { public nint hProcess, hThread; public int processId, threadId; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateDesktop(string name, nint device, nint devmode, int flags, uint access, nint attributes);
    [DllImport("user32.dll")] internal static extern bool CloseDesktop(nint desktop);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool CreateProcess(string application, StringBuilder command, nint processAttributes,
        nint threadAttributes, bool inheritHandles, uint flags, nint environment, string? directory,
        ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] internal static extern nint GetStdHandle(int which);
    [DllImport("kernel32.dll")] internal static extern uint WaitForSingleObject(nint handle, uint ms);
    [DllImport("kernel32.dll")] internal static extern bool GetExitCodeProcess(nint process, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool TerminateProcess(nint process, uint code);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
}
