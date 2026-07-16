using System.Windows;

namespace ScreenRecorder;

public partial class App : System.Windows.Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"应用错误: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"严重错误: {ex.Message}\n\n{ex.StackTrace}", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        MessageBox.Show($"任务错误: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.SetObserved();
    }
}
