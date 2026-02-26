using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using YouTubeStuff.Dialogs;
using YouTubeStuff.SettingsManager;

namespace YouTubeStuff {

    public class Config : SettingsManager<Config> {
        public int ExportType { get; set; } = 0;
        public int ExportFormatAudio { get; set; } = 0;
        public int ExportFormatVideo { get; set; } = 0;
        public int Subtitles { get; set; } = 0;
        public int SubtitleFontSize { get; set; } = 16;
        public int MaxConcurrentDownloads { get; set; } = 6;

        public bool UpdateChecker { get; set; } = true;
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
        public static readonly string AppName = Assembly.GetEntryAssembly().GetName().Name;

        public App() {
            Config.Load();

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            Directory.CreateDirectory(Config.Settings.OutDir);
            Directory.CreateDirectory(Config.Settings.UtilsDir);

            DispatcherUnhandledException += Application_DispatcherUnhandledException;
        }

        protected override async void OnStartup(StartupEventArgs e) {
            MainWindow mw = new();

            if (await CheckUtils())
                new DownloadWindow().ShowDialog();

            mw.Show();

            if (Config.Settings.UpdateChecker)
                await CheckVersion("Dyvinia", "YouTubeStuff");
        }

        // returns true if needs update/download
        private static async Task<bool> CheckUtils() {
            if (File.Exists(Config.Settings.UtilsDir + "yt-dlp.exe")) {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", "request");

                dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest"));

                Version local = new(FileVersionInfo.GetVersionInfo(Config.Settings.UtilsDir + "yt-dlp.exe").FileVersion);
                Version latest = new((string)json.tag_name);

                if (local.CompareTo(latest) < 0)
                    return true;
            }
            else
                return true;

            if (!File.Exists(Config.Settings.UtilsDir + "ffmpeg.exe"))
                return true;

            return false;
        }

        public static async Task CheckVersion(string repoAuthor, string repoName) {
            try {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", "request");

                dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync($"https://api.github.com/repos/{repoAuthor}/{repoName}/releases/latest"));
                Version latest = new(((string)json.tag_name)[1..]);
                Version local = Assembly.GetExecutingAssembly().GetName().Version;

                if (local.CompareTo(latest) < 0) {
                    string message = $"You are using {AppName} v{local.ToString()[..5]}. \nWould you like to download the latest version? (v{latest})";
                    MessageBoxResult Result = MessageBoxDialog.Show(message, "FrostyFix", MessageBoxButton.YesNo, DialogSound.Notify);
                    if (Result == MessageBoxResult.Yes) {
                        Process.Start(new ProcessStartInfo($"https://github.com/{repoAuthor}/{repoName}/releases/latest") { UseShellExecute = true });
                    }
                }
            }
            catch (Exception e) {
                ExceptionDialog.Show(e, AppName, false, "Unable to check for updates:");
            }
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            e.Handled = true;
            string title = "YouTubeStuff";
            ExceptionDialog.Show(e.Exception, title, true);
        }
    }
}
