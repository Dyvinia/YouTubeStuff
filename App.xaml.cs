using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using YouTubeStuff.Dialogs;
using YouTubeStuff.SettingsManager;

namespace YouTubeStuff {

    public class Config : SettingsManager<Config> {
        public int ExportType { get; set; } = 0;
        public int ExportFormatAudio { get; set; } = 0;
        public int ExportFormatVideo { get; set; } = 0;

        public bool ShowWindows { get; set; } = false;
        public bool PasteOnStartup { get; set; } = true;

        public string AdditionalArgs { get; set; } = "";
        public string OutDir { get; set; } = App.BaseDir + "Output";
        public string UtilsDir { get; set; } = App.BaseDir + "Utils\\";
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static readonly string Version = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString()[..5];
        public static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

        public App() {
            Config.Load();

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            Directory.CreateDirectory(Config.Settings.OutDir);
            Directory.CreateDirectory(Config.Settings.UtilsDir);

            DispatcherUnhandledException += Application_DispatcherUnhandledException;
        }

        protected override async void OnStartup(StartupEventArgs e) {
            MainWindow = new MainWindow();
            if (!File.Exists(Config.Settings.UtilsDir + "yt-dlp.exe"))
                await ShowPopup(new DownloadWindow());
            MainWindow.Show();
        }

        private Task ShowPopup<TPopup>(TPopup popup) where TPopup : Window {
            var task = new TaskCompletionSource<object>();
            popup.Closed += (s, a) => task.SetResult(null);
            popup.Show();
            popup.Focus();
            return task.Task;
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            e.Handled = true;
            string title = "YouTubeStuff";
            ExceptionDialog.Show(e.Exception, title, true);
        }
    }
}
