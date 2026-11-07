// Startprogramm fuer Zwift unter Wine/Proton, liegt im Zwift-Verzeichnis.
// Aufruf: ZwiftStart.exe [--update-only | --no-update]
//   Prueft die Version gegen das Zwift-CDN und aktualisiert bei Bedarf ueber
//   "ZwiftLauncher.exe SilentLaunch". Danach startet es ZwiftApp.exe als
//   Kindprozess des Launchers, beendet den Launcher und wartet auf das Spiel.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

internal static class ZwiftStart
{
    private const string VersionUrl = "https://cdn.zwift.com/gameassets/Zwift_Updates_Root/Zwift_ver_cur.xml";
    private const int UpdatePollSeconds = 5;
    // Bei der Erstinstallation laedt der Launcher das ganze Spiel (mehrere GB).
    private const int UpdateTimeoutMinutes = 60;
    private const int UpdatePollCount = UpdateTimeoutMinutes * 60 / UpdatePollSeconds;

    private static readonly string ZwiftDir = AppDomain.CurrentDomain.BaseDirectory;
    // Unter umu/Proton erscheint die Konsolenausgabe nicht; daher zusaetzlich in eine Datei.
    private static readonly string LogFile = Path.Combine(ZwiftDir, "ZwiftStart.log");

    private static void Log(string message)
    {
        string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message;
        Console.WriteLine(line);
        try { File.AppendAllText(LogFile, line + Environment.NewLine); }
        catch (IOException) { }
    }

    private static int Main(string[] args)
    {
        bool updateOnly = Array.IndexOf(args, "--update-only") >= 0;
        bool noUpdate = Array.IndexOf(args, "--no-update") >= 0;
        try
        {
            File.Delete(LogFile);
            if (!File.Exists(Path.Combine(ZwiftDir, "ZwiftLauncher.exe")))
                throw new ApplicationException("ZwiftLauncher.exe fehlt in " + ZwiftDir);

            if (!noUpdate)
            {
                bool updated = Update(updateOnly);
                if (updateOnly)
                {
                    KillAll("ZwiftLauncher");
                    KillAll("ZwiftWindowsCrashHandler");
                    return updated ? 0 : 1;
                }
            }
            return Start();
        }
        catch (Exception e)
        {
            Log("ZwiftStart: " + e.Message);
            return 1;
        }
    }

    // Liefert false, wenn das Update fehlgeschlagen ist. Ohne CDN-Zugriff wird
    // beim normalen Start mit der installierten Version weitergemacht.
    private static bool Update(bool required)
    {
        string current = CurrentVersion();
        string latest;
        try
        {
            latest = LatestVersion();
        }
        catch (Exception e)
        {
            if (required) throw new ApplicationException("CDN-Version nicht lesbar: " + e.Message);
            Log("Warnung: CDN-Version nicht lesbar (" + e.Message + "), starte ohne Update.");
            return false;
        }

        if (current == latest)
        {
            Log("Zwift ist aktuell: " + current);
            return true;
        }

        Log("Aktualisiere Zwift von " + current + " auf " + latest + " ...");
        StartLauncher();
        string updated = current;
        for (int i = 0; i < UpdatePollCount && updated == current; i++)
        {
            if (Process.GetProcessesByName("ZwiftLauncher").Length == 0)
                throw new ApplicationException("Der Launcher wurde vor Abschluss des Updates beendet.");
            Thread.Sleep(UpdatePollSeconds * 1000);
            updated = CurrentVersion();
        }
        if (updated == current)
            throw new ApplicationException("Zeitueberschreitung beim Zwift-Update (" + UpdateTimeoutMinutes + " Minuten).");
        if (updated != latest)
            throw new ApplicationException("Unerwartete Zwift-Version nach dem Update: " + updated + " statt " + latest);
        Log("Zwift wurde auf Version " + updated + " aktualisiert.");
        return true;
    }

    private static int Start()
    {
        if (!File.Exists(Path.Combine(ZwiftDir, "ZwiftApp.exe")))
            throw new ApplicationException("ZwiftApp.exe fehlt; das Spiel ist noch nicht heruntergeladen.");

        CreateProfileFolders();
        EnableFullscreenDefault();

        Process launcher = FindProcess("ZwiftLauncher");
        if (launcher == null)
        {
            Log("Starte ZwiftLauncher.exe im Hintergrund ...");
            StartLauncher();
            launcher = WaitForProcess("ZwiftLauncher", 10);
        }
        Log("Launcher-PID: " + launcher.Id);

        // Ohne den Launcher als Elternprozess startet ZwiftApp.exe den Launcher
        // neu und beendet sich selbst.
        Log("Starte ZwiftApp.exe als Kindprozess des Launchers ...");
        DateTime started = DateTime.Now.AddSeconds(-2);
        int appPid = StartWithParent(launcher.Id, "\"" + Path.Combine(ZwiftDir, "ZwiftApp.exe") + "\"", ZwiftDir);
        Log("ZwiftApp-PID: " + appPid);
        WaitForProcess("ZwiftApp", 10);

        // Der Launcher darf erst weg, wenn ZwiftApp.exe ihn als Elternprozess
        // erkannt hat; sonst startet es einen neuen Launcher und beendet sich.
        if (WaitForGameLog(started, 120))
            Log("ZwiftApp.exe meldet \"Legacy Launcher Used\".");
        else
            Log("Warnung: \"Legacy Launcher Used\" nicht im Log.txt gefunden.");

        KillAll("ZwiftLauncher");
        KillAll("ZwiftWindowsCrashHandler");
        Log("Zwift wurde gestartet; der Launcher wurde beendet.");

        while (Process.GetProcessesByName("ZwiftApp").Length > 0)
            Thread.Sleep(5000);

        KillAll("ZwiftLauncher");
        KillAll("ZwiftWindowsCrashHandler");
        Log("Zwift wurde beendet.");
        return 0;
    }

    private static void StartLauncher()
    {
        var info = new ProcessStartInfo(Path.Combine(ZwiftDir, "ZwiftLauncher.exe"), "SilentLaunch");
        info.WorkingDirectory = ZwiftDir;
        info.UseShellExecute = false;
        Process.Start(info);
    }

    // Fehlen Standardordner im Benutzerprofil, beendet sich ZwiftApp.exe sofort.
    private static void CreateProfileFolders()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (string folder in new[] { "Contacts", "Desktop", "Documents", "Downloads", "Favorites", "Links",
                     "Music", @"Pictures\Screenshots", "Saved Games", "Searches", "Videos" })
            Directory.CreateDirectory(Path.Combine(profile, folder));
    }

    // Zwift startet sonst im Fenster mit Titelleiste. Nur setzen, wenn der
    // Schluessel fehlt, damit eine Umstellung im Spiel erhalten bleibt.
    private static void EnableFullscreenDefault()
    {
        string prefs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Zwift\prefs.xml");
        const string entry = "<FULLSCREEN>1</FULLSCREEN>";
        try
        {
            if (!File.Exists(prefs))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(prefs));
                File.WriteAllText(prefs, "<ZWIFT>\n    <CONFIG>\n        " + entry + "\n    </CONFIG>\n</ZWIFT>\n");
            }
            else
            {
                string xml = File.ReadAllText(prefs);
                if (xml.Contains("<FULLSCREEN>")) return;
                if (xml.Contains("</CONFIG>"))
                    xml = xml.Replace("</CONFIG>", "    " + entry + "\n    </CONFIG>");
                else if (xml.Contains("</ZWIFT>"))
                    xml = xml.Replace("</ZWIFT>", "    <CONFIG>\n        " + entry + "\n    </CONFIG>\n</ZWIFT>");
                else
                    return;
                File.WriteAllText(prefs, xml);
            }
            Log("Vollbild in prefs.xml voreingestellt.");
        }
        catch (Exception e)
        {
            Log("Warnung: prefs.xml nicht angepasst (" + e.Message + ").");
        }
    }

    private static string CurrentVersion()
    {
        string versionFile = "Zwift_ver_cur.xml";
        string pointer = Path.Combine(ZwiftDir, "Zwift_ver_cur_filename.txt");
        try
        {
            if (File.Exists(pointer))
            {
                string name = File.ReadAllText(pointer).Replace("\0", "").Split('\r', '\n')[0].Trim();
                if (name.Length > 0) versionFile = name;
            }
            return ParseVersion(File.ReadAllText(Path.Combine(ZwiftDir, versionFile))) ?? "none";
        }
        catch (IOException)
        {
            return "none";
        }
    }

    private static string LatestVersion()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        using (var client = new WebClient())
        {
            string version = ParseVersion(client.DownloadString(VersionUrl));
            if (string.IsNullOrEmpty(version)) throw new ApplicationException("keine Version in " + VersionUrl);
            return version;
        }
    }

    private static string ParseVersion(string xml)
    {
        Match match = Regex.Match(xml, "sversion=\"([^\"\\s]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static Process FindProcess(string name)
    {
        Process[] processes = Process.GetProcessesByName(name);
        return processes.Length > 0 ? processes[0] : null;
    }

    private static Process WaitForProcess(string name, int seconds)
    {
        for (int i = 0; i < seconds * 10; i++)
        {
            Process process = FindProcess(name);
            if (process != null) return process;
            Thread.Sleep(100);
        }
        throw new ApplicationException("Zeitueberschreitung beim Warten auf " + name + ".exe.");
    }

    private static bool WaitForGameLog(DateTime since, int seconds)
    {
        string log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Zwift\Logs\Log.txt");
        for (int i = 0; i < seconds * 2; i++)
        {
            if (Process.GetProcessesByName("ZwiftApp").Length == 0)
                throw new ApplicationException("ZwiftApp.exe hat sich vorzeitig beendet.");
            try
            {
                if (File.Exists(log) && File.GetLastWriteTime(log) >= since)
                {
                    using (var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream))
                    {
                        if (reader.ReadToEnd().Contains("Legacy Launcher Used")) return true;
                    }
                }
            }
            catch (IOException) { }
            Thread.Sleep(500);
        }
        return false;
    }

    private static void KillAll(string name)
    {
        foreach (Process process in Process.GetProcessesByName(name))
        {
            try { process.Kill(); }
            catch (Exception) { }
        }
    }

    // CreateProcess mit PROC_THREAD_ATTRIBUTE_PARENT_PROCESS (wie nirsoft RunFromProcess).
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

    private static int StartWithParent(int ppid, string commandLine, string currentDirectory)
    {
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
            if (!CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                    EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT, IntPtr.Zero, currentDirectory,
                    ref startupInfo, out processInformation))
                throw new Win32Exception();

            CloseHandle(processInformation.hThread);
            CloseHandle(processInformation.hProcess);
            return processInformation.dwProcessId;
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
