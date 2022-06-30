using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Cache;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace YouTubeStuff {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        public class Video {
            public string Title { get; set; }
            public string Link { get; set; }
            public string Thumbnail { get; set; }
            public string Site { get; set; }
        }
        public ObservableCollection<Video> Videos = new();

        public MainWindow() {
            InitializeComponent();

            LinkBox.LostFocus += (s, e) => LinkChanged();

            VideoListBox.ItemsSource = Videos;
            VideoListBox.SelectionChanged += (s, e) => UpdateUI();

            MouseDown += (s, e) => FocusManager.SetFocusedElement(this, this);
        }

        private async void LinkChanged() {
            string[] strings = String.Concat(LinkBox.Text.Where(c => !Char.IsWhiteSpace(c))).Split(",");
            string[] links = strings.Where(s => Uri.IsWellFormedUriString(s, UriKind.Absolute)).ToArray();

            IProgress<int> progress = new Progress<int>(p => {
                ProgressBar.Value = p;
                ProgressBar.Maximum = strings.Length;
            });

            ProgressBar.Visibility = Visibility.Visible;
            await GenerateList(strings, progress);
            ProgressBar.Visibility = Visibility.Collapsed;

            if (Videos.Count > 0) VideoListBox.SelectedIndex = 0;

            UpdateUI();
        }

        private async Task GenerateList(string[] links, IProgress<int> progress) {
            Videos.Clear();
            int currentProgress = 1;

            foreach (string link in links) {
                // YouTube
                if (link.Contains("youtu")) {
                    using HttpClient client = new();
                    dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync($"https://noembed.com/embed?url={link}"));
                    string videoTitle = json.title;
                    string videoID = ((string)json.thumbnail_url).Split("/")[^2];

                    Videos.Add(new Video { Title = videoTitle, Link = link, Thumbnail = $"https://img.youtube.com/vi/{videoID}/maxresdefault.jpg", Site = "YouTube" });
                }
                // Twitter
                if (link.Contains("twitter.com")) {
                    ProcessStartInfo p = new() {
                        FileName = App.GDL,
                        CreateNoWindow = true,
                        Arguments = $"--dump-json " + link,
                        RedirectStandardOutput = true,
                    };
                    Process process = Process.Start(p);
                    string output = process.StandardOutput.ReadToEnd();
                    dynamic results = JsonConvert.DeserializeObject<dynamic>(output);
                    string userImage = results[0][1].user.profile_image;
                    string tweetContent = results[0][1].content;
                    tweetContent = tweetContent.Replace("\n", "").Replace("\r", "");

                    Videos.Add(new Video { Title = tweetContent, Link = link, Thumbnail = userImage, Site = "Twitter" });
                }
                // Reddit
                if (link.Contains("reddit.com")) {
                    using HttpClient client = new();
                    dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync($"{link}.json"));
                    string videoTitle = json[0].data.children[0].data.title;
                    string videoThumbnail = json[0].data.children[0].data.thumbnail;

                    Videos.Add(new Video { Title = videoTitle, Link = link, Thumbnail = videoThumbnail, Site = "Reddit" });
                }

                progress.Report(currentProgress++);
            }
        }

        private void UpdateUI() {
            if (VideoListBox.SelectedItem is not Video video) {
                ImageThumbnail.Source = null;
                TitleBox.Text = null;
                return;
            }

            ImageThumbnail.Source = new BitmapImage(new Uri(video.Thumbnail));
            TitleBox.Text = video.Title;

            if (String.IsNullOrEmpty(video.Thumbnail)) {
                ButtonClipboard.IsEnabled = false;
                ButtonSave.IsEnabled = false;
            }
            else {
                ButtonClipboard.IsEnabled = true;
                ButtonSave.IsEnabled = true;
            }
        }

        private void Download(Video video) {
            using Process downloader = new();
            if (video.Site == "YouTube") {
                downloader.StartInfo.FileName = App.YTDL;
                //p.Arguments = $"-f bestaudio -x --audio-format flac {video.Link} -o \"{App.OutDir}\\%%(title)s.%%(ext)s\"";
                //p.Arguments = $"-f bestaudio -x --audio-format mp3 {video.Link} -o \"{App.OutDir}\\%%(title)s.%%(ext)s\"";
                downloader.StartInfo.Arguments = $"--format \"bestvideo+bestaudio[ext=m4a]/bestvideo+bestaudio/best\" --merge-output-format mp4  {video.Link} -o \"{App.OutDir}\\{video.Title}\"";

            }
            else if (video.Site == "Twitter") {
                downloader.StartInfo.FileName = App.GDL;
                downloader.StartInfo.Arguments = $"-D \"{App.OutDir}\" \"{video.Link}\"";
            }
            else if (video.Site == "Reddit") {
                downloader.StartInfo.FileName = App.YTDL;
                downloader.StartInfo.Arguments = $"{video.Link} -o \"{App.OutDir}\\{video.Title}.%(ext)s\"";
            }
            downloader.Start();
            downloader.WaitForExit();
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e) {
            Video video = VideoListBox.SelectedItem as Video;
            SaveFileDialog saveFileDialog = new() { InitialDirectory = App.OutDir, Filter = "Image|*.*" };
            if (saveFileDialog.ShowDialog() == true) {
                using HttpClient httpClient = new();
                string fileExtension = Path.GetExtension(video.Thumbnail);
                string finalPath = $"{saveFileDialog.FileName}{fileExtension}";

                byte[] imageBytes = await httpClient.GetByteArrayAsync(new Uri(video.Thumbnail));
                await File.WriteAllBytesAsync(finalPath, imageBytes);
            }
        }

        private void ButtonClipboard_Click(object sender, RoutedEventArgs e) {
            Video video = VideoListBox.SelectedItem as Video;
            IDataObject data = new DataObject();
            data.SetData(DataFormats.Bitmap, new BitmapImage(new Uri(video.Thumbnail)), true);
            Clipboard.SetDataObject(data, true);
        }

        private void DownloadAllButton_Click(object sender, RoutedEventArgs e) {
            Parallel.ForEach(Videos, video => {
                Download(video);
            });
            Process.Start(new ProcessStartInfo(App.OutDir) { UseShellExecute = true });
        }

        private void ListBoxDownloadVideo_Click(object sender, RoutedEventArgs e) {
            Video video = ((Button)sender).DataContext as Video;
            Download(video);
            Process.Start(new ProcessStartInfo(App.OutDir) { UseShellExecute = true });
        }
    }
}
