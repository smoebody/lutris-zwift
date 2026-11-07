// Startet ein Programm mit einem fremden Prozess als Elternprozess.
// Aufruf: RunFromProcess.exe <ppid> <kommandozeile ...>
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

internal static class RunFromProcess
{
    private const uint PROCESS_CREATE_PROCESS = 0x0080;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const int STARTF_USESHOWWINDOW = 0x00000001;
    private const short SW_SHOW = 5;
    private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = (IntPtr)0x00020000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute,
        IntPtr value, IntPtr size, IntPtr previousValue, IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(string application, string commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint creationFlags, IntPtr environment,
        string currentDirectory, ref STARTUPINFOEX startupInfo, out PROCESS_INFORMATION processInformation);

    private static int Main(string[] args)
    {
        int ppid;
        if (args.Length < 2 || !int.TryParse(args[0], out ppid))
        {
            Console.Error.WriteLine("Usage: RunFromProcess.exe <ppid> <commandline>");
            return 2;
        }

        IntPtr parent = IntPtr.Zero;
        IntPtr list = IntPtr.Zero;
        IntPtr parentValue = IntPtr.Zero;
        try
        {
            parent = OpenProcess(PROCESS_CREATE_PROCESS, false, ppid);
            if (parent == IntPtr.Zero) throw new Win32Exception();

            IntPtr size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            list = Marshal.AllocHGlobal(size);
            if (!InitializeProcThreadAttributeList(list, 1, 0, ref size)) throw new Win32Exception();

            parentValue = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(parentValue, parent);
            if (!UpdateProcThreadAttribute(list, 0, PROC_THREAD_ATTRIBUTE_PARENT_PROCESS, parentValue,
                    (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception();

            var startupInfo = new STARTUPINFOEX();
            startupInfo.StartupInfo.cb = Marshal.SizeOf(typeof(STARTUPINFOEX));
            startupInfo.StartupInfo.dwFlags = STARTF_USESHOWWINDOW;
            startupInfo.StartupInfo.wShowWindow = SW_SHOW;
            startupInfo.lpAttributeList = list;

            PROCESS_INFORMATION processInformation;
            if (!CreateProcess(null, string.Join(" ", args, 1, args.Length - 1), IntPtr.Zero, IntPtr.Zero, false,
                    EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT, IntPtr.Zero, null,
                    ref startupInfo, out processInformation))
                throw new Win32Exception();

            CloseHandle(processInformation.hThread);
            CloseHandle(processInformation.hProcess);
            Console.WriteLine(processInformation.dwProcessId);
            return 0;
        }
        catch (Win32Exception e)
        {
            Console.Error.WriteLine("RunFromProcess: " + e.Message + " (" + e.NativeErrorCode + ")");
            return 1;
        }
        finally
        {
            if (list != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(list);
                Marshal.FreeHGlobal(list);
            }
            if (parentValue != IntPtr.Zero) Marshal.FreeHGlobal(parentValue);
            if (parent != IntPtr.Zero) CloseHandle(parent);
        }
    }
}
