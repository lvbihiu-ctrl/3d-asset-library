using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace AssetLibraryLauncher;

internal static class Program
{
    private const string InstallRoot = @"D:\Codex\3D-Asset-Library-EXE";
    private const string MainExeName = "3D素材管理器.exe";
    private const string MutexName = @"Local\lvbihiu-ctrl.3d-asset-library";
    private static readonly string RuntimeRoot = Path.Combine(InstallRoot, ".runtime");
    private static readonly string LogPath = Path.Combine(InstallRoot, "logs", "launcher.log");

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(InstallRoot);
            Directory.CreateDirectory(RuntimeRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

            var waitPid = ReadPidArgument(args, "--restart-after");
            if (waitPid > 0)
            {
                WaitForProcess(waitPid, TimeSpan.FromMinutes(2));
            }

            var currentExe = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法确定当前 EXE 路径。");
            var mainExe = Path.Combine(InstallRoot, MainExeName);

            if (!SamePath(currentExe, mainExe) && !args.Contains("--run-portable"))
            {
                return QueueInstall(currentExe, mainExe);
            }

            using var mutex = new Mutex(false, MutexName);
            if (!AcquireMutex(mutex, TimeSpan.FromSeconds(waitPid > 0 ? 15 : 0)))
            {
                Log("Another instance is already running; exiting.");
                return 0;
            }

            try
            {
                var script = ResolveApplicationScript();
                Log($"Starting script: {script}");
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        @"WindowsPowerShell\v1.0\powershell.exe"),
                    Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -STA -File \"{script}\"",
                    WorkingDirectory = InstallRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }) ?? throw new InvalidOperationException("无法启动 PowerShell 客户端。");
                process.WaitForExit();
                Log($"Application script exited with code {process.ExitCode}.");
                return process.ExitCode;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        catch (Exception ex)
        {
            Log("Fatal: " + ex);
            ShowError("3D 素材管理器启动失败：\r\n" + ex.Message + "\r\n\r\n日志：" + LogPath);
            return 1;
        }
    }

    private static int QueueInstall(string sourceExe, string mainExe)
    {
        var staged = mainExe + ".new";
        File.Copy(sourceExe, staged, true);
        Log($"Staged launcher {sourceExe} -> {staged}");

        var helper = Path.Combine(RuntimeRoot, "finish-launcher-update.ps1");
        File.WriteAllText(helper, BuildInstallHelper(), new UTF8Encoding(false));
        var ps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"WindowsPowerShell\v1.0\powershell.exe");
        _ = Process.Start(new ProcessStartInfo
        {
            FileName = ps,
            Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{helper}\" -ParentPid {Environment.ProcessId} -Staged \"{staged}\" -Target \"{mainExe}\"",
            WorkingDirectory = InstallRoot,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("无法启动安装辅助进程。");
        return 0;
    }

    private static string BuildInstallHelper() => """
param([int]$ParentPid,[string]$Staged,[string]$Target)
$ErrorActionPreference='Stop'
$log='D:\Codex\3D-Asset-Library-EXE\logs\launcher-update.log'
function Log([string]$m){Add-Content -LiteralPath $log -Encoding UTF8 -Value ((Get-Date -Format o)+' '+$m)}
try {
  try {Wait-Process -Id $ParentPid -Timeout 120 -ErrorAction Stop} catch {}
  $deadline=(Get-Date).AddMinutes(5)
  while($true){
    $targetIsRunning=@(Get-Process -ErrorAction SilentlyContinue | Where-Object {
      try {$_.Path -and $_.Path.Equals($Target,[StringComparison]::OrdinalIgnoreCase)} catch {$false}
    }).Count -gt 0
    if(-not $targetIsRunning){break}
    if((Get-Date) -ge $deadline){throw 'Timed out waiting for the running launcher to exit.'}
    Start-Sleep -Milliseconds 500
  }
  while($true){
    try {
      if(Test-Path -LiteralPath $Target){
        $previous=$Target+'.previous'
        if(Test-Path -LiteralPath $previous){Remove-Item -LiteralPath $previous -Force}
        [IO.File]::Replace($Staged,$Target,$previous,$true)
      }
      else {[IO.File]::Move($Staged,$Target)}
      break
    } catch [IO.IOException] {
      if((Get-Date) -ge $deadline){throw}
      Start-Sleep -Milliseconds 500
    } catch [UnauthorizedAccessException] {
      if((Get-Date) -ge $deadline){throw}
      Start-Sleep -Milliseconds 500
    }
  }
  Log ('Installed '+$Target)
  Start-Process -FilePath $Target -ArgumentList @('--restart-after',[string]$ParentPid) -WorkingDirectory (Split-Path $Target -Parent)
} catch {Log ('Update failed: '+$_.Exception.ToString())}
""";

    private static string ResolveApplicationScript()
    {
        var overrideScript = Path.Combine(RuntimeRoot, "override.ps1");
        var overrideHash = Path.Combine(RuntimeRoot, "override.sha256");
        if (File.Exists(overrideScript) && File.Exists(overrideHash))
        {
            var expected = File.ReadAllText(overrideHash).Trim();
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(overrideScript)));
            if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                return overrideScript;
            }
            Log("Ignoring override.ps1 because SHA256 verification failed.");
        }

        var versioned = Directory.EnumerateFiles(RuntimeRoot, "app-v*.ps1")
            .Select(path => new { Path = path, Version = ParseScriptVersion(path) })
            .Where(item => item.Version is not null)
            .OrderByDescending(item => item.Version)
            .FirstOrDefault();
        if (versioned is not null)
        {
            return versioned.Path;
        }

        var fallback = Path.Combine(RuntimeRoot, "app.ps1");
        if (File.Exists(fallback))
        {
            return fallback;
        }
        throw new FileNotFoundException("客户端脚本缺失，请保留安装目录下的 .runtime 文件夹。", fallback);
    }

    private static Version? ParseScriptVersion(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.StartsWith("app-v", StringComparison.OrdinalIgnoreCase)
            && Version.TryParse(name[5..], out var version) ? version : null;
    }

    private static bool AcquireMutex(Mutex mutex, TimeSpan timeout)
    {
        try { return mutex.WaitOne(timeout); }
        catch (AbandonedMutexException) { return true; }
    }

    private static int ReadPidArgument(string[] args, string name)
    {
        for (var i = 0; i + 1 < args.Length; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out var pid)) return pid;
        }
        return 0;
    }

    private static void WaitForProcess(int pid, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException) { }
    }

    private static bool SamePath(string left, string right) =>
        Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} v{Assembly.GetExecutingAssembly().GetName().Version} {message}{Environment.NewLine}", new UTF8Encoding(false));
        }
        catch { }
    }

    private static void ShowError(string message)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "mshta.exe",
                Arguments = $"javascript:alert('{EscapeJs(message)}');close()",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private static string EscapeJs(string value) => value
        .Replace("\\", "\\\\")
        .Replace("'", "\\'")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");
}
