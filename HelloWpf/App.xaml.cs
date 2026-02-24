using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace HelloWpf;

public partial class App : Application
{
    private readonly string _logPath = Path.Combine(Path.GetTempPath(), "NxERP-startup.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            base.OnStartup(e);
            WriteInfo("Startup begin");
            EnsureDesktopShortcut();

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            WriteInfo("Main window shown");
        }
        catch (Exception ex)
        {
            HandleFatal(ex, "Startup failure");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        HandleFatal(e.Exception, "UI thread crash");
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            HandleFatal(ex, "Background crash");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleFatal(e.Exception, "Task crash");
        e.SetObserved();
    }

    private void HandleFatal(Exception ex, string title)
    {
        try
        {
            File.AppendAllText(_logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }

        MessageBox.Show(
            $"{title}.\n\n{ex.Message}\n\nLog file: {_logPath}",
            "NxERP Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Shutdown(-1);
    }

    private void WriteInfo(string message)
    {
        try
        {
            var exePath = Environment.ProcessPath ?? "unknown";
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO {message} | Version={version} | Exe={exePath}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void EnsureDesktopShortcut()
    {
        try
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktopPath))
            {
                return;
            }

            var shortcutPath = Path.Combine(desktopPath, "NxERP.lnk");
            if (File.Exists(shortcutPath))
            {
                return;
            }

            var targetPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
            {
                return;
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            object? shell = null;
            object? shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [shortcutPath]);
                if (shortcut is null)
                {
                    return;
                }

                var shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [targetPath]);
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [Path.GetDirectoryName(targetPath)]);
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, [$"{targetPath},0"]);
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, ["NxERP Open Source"]);
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut is not null && Marshal.IsComObject(shortcut))
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }

                if (shell is not null && Marshal.IsComObject(shell))
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
        }
        catch
        {
        }
    }
}
