using System;
using System.Collections.Generic;
using System.Configuration;
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
        public string AdditionalArgs { get; set; }

        public string OutDir { get; set; } = App.BaseDir + "Output";
        public string YoutubeDL { get; set; } = App.BaseDir + "yt-dlp.exe";
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static readonly string Version = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString()[..5];

        public static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        

        public App() {
            Config.Load();

            Directory.CreateDirectory(Config.Settings.OutDir);

            DispatcherUnhandledException += Application_DispatcherUnhandledException;
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            e.Handled = true;
            string title = "YouTubeStuff";
            ExceptionDialog.Show(e.Exception, title, true);
        }
    }
}
