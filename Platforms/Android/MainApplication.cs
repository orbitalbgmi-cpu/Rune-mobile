using Android.App;
using Android.Runtime;

namespace RuneMobile;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
        AndroidEnvironment.UnhandledExceptionRaiser += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnUnhandledException(object? sender, RaiseThrowableEventArgs e)
    {
        WriteCrashLog(e.Exception.ToString());
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.ExceptionObject?.ToString() ?? "Unknown crash");
    }

    private void WriteCrashLog(string content)
    {
        try
        {
            var dir = GetExternalFilesDir(null)!.AbsolutePath;
            var path = Path.Combine(dir, "crash-log.txt");
            File.WriteAllText(path, $"{DateTime.Now}\n{content}");
        }
        catch { }
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
