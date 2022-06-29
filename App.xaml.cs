using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace YouTubeStuff {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static readonly string Version = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString().Substring(0, 5);

        public static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string OutDir = BaseDir + "Output";

        public App() {

            Directory.CreateDirectory(OutDir);
        }
    }
}
