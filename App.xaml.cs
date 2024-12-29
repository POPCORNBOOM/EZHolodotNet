using System.Configuration;
using System.Data;
using System.Windows;

namespace EZHolodotNet
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // 订阅未处理异常事件
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // 记录异常信息
            string errorMessage = $"发生未处理的异常: {e.Exception.Message}\n{e.Exception.StackTrace}";

            // 可以选择记录到文件，或者显示给用户
            LogError(errorMessage);  // 自定义的日志记录方法
            MessageBox.Show("errorMessage");

            // 设置为 true 表示异常已经被处理，应用程序不会崩溃
            e.Handled = true;

            // 如果需要崩溃应用程序，设置为 false
            // e.Handled = false;
        }

        private void LogError(string message)
        {
            // 这里可以记录日志到文件或者其他日志管理工具
            System.IO.File.AppendAllText("error_log.txt", message);
        }
    }

}
