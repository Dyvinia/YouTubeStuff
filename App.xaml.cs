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
using PropertyChanged;
using YouTubeStuff.Dialogs;
using YouTubeStuff.SettingsManager;

namespace YouTubeStuff {

    [AddINotifyPropertyChangedInterface]
    public class Config : SettingsManager<Config> {
        public int ExportType { get; set; } = 0;
        public int ExportFormatAudio { get; set; } = 0;
        public int ExportFormatVideo { get; set; } = 0;
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static readonly string Version = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString()[..5];

        public static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string OutDir = BaseDir + "Output";
        public static readonly string YTDL = BaseDir + "yt-dlp.exe";
        public static readonly string GDL = BaseDir + "gallery-dl.exe";

        public App() {
            Config.Load();

            Directory.CreateDirectory(OutDir);

            DispatcherUnhandledException += Application_DispatcherUnhandledException;
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            e.Handled = true;
            string title = "YouTubeStuff";
            ExceptionDialog.Show(e.Exception, title, true);
        }
    }
}
