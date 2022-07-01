using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Net.Http;
using System.IO.Compression;
using System.Windows.Shell;

namespace YouTubeStuff {
    /// <summary>
    /// Interaction logic for DownloadWindow.xaml
    /// </summary>
    public partial class DownloadWindow : Window {
        public static readonly string TempDir = Path.Combine(Config.Settings.UtilsDir, ".temp");

        public DownloadWindow() {
            InitializeComponent();
            Startup();
        }

        public async void Startup() {

            IProgress<double> progress = new Progress<double>(p => {
                pbStatus.Value = p;
                TaskbarItemInfo.ProgressValue = p;
            });

            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            Directory.CreateDirectory(TempDir);
            await DownloadFFmpeg(progress);
            await DownloadYTDL(progress);
            await Task.Delay(200);
            Directory.Delete(TempDir, true);
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

            this.Close();
        }

        public async static Task DownloadFFmpeg(IProgress<double> progress) {
            string destination = $"{TempDir}\\ffmpeg.zip";
            string url = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

            await Download(url, destination, progress);

            using ZipArchive archive = ZipFile.OpenRead(destination);
            foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName.Contains("ffmpeg.exe"))) {
                entry.ExtractToFile(Path.Combine(App.BaseDir, "Utils", entry.Name), true);
            }
        }

        public async static Task DownloadYTDL(IProgress<double> progress) {
            string destination = App.BaseDir + "Utils\\yt-dlp.exe";
            string url = "https://github.com/yt-dlp/yt-dlp/releases/download/2022.06.29/yt-dlp.exe";

            await Download(url, destination, progress);
        }

        public async static Task Download(string downloadUrl, string destinationFilePath, IProgress<double> progress) {
            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(30) };
            using HttpResponseMessage response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();
            long? totalBytes = response.Content.Headers.ContentLength;

            using Stream contentStream = await response.Content.ReadAsStreamAsync();
            long? totalBytesRead = 0L;
            long readCount = 0L;
            byte[] buffer = new byte[4096];
            bool isMoreToRead = true;

            using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

            do {
                int bytesRead = await contentStream.ReadAsync(buffer);
                if (bytesRead == 0) {
                    isMoreToRead = false;
                    progress.Report((double)((double)totalBytesRead/totalBytes));
                    continue;
                }

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));

                totalBytesRead += bytesRead;
                readCount++;

                if (readCount % 100 == 0) {
                    progress.Report((double)((double)totalBytesRead / totalBytes));
                }
            }
            while (isMoreToRead);
        }
    }
}
