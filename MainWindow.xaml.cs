using HtmlAgilityPack;
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

            ExtensionComboBox.SelectionChanged += ExtensionComboBox_SelectionChanged; ;
            FormatAudioComboBox.SelectionChanged += (s, e) => Config.Save();
            FormatVideoComboBox.SelectionChanged += (s, e) => Config.Save();
            ExtensionComboBox.DataContext = Config.Settings;
            FormatAudioComboBox.DataContext = Config.Settings;
            FormatVideoComboBox.DataContext = Config.Settings;

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
            Mouse.OverrideCursor = Cursors.Wait;
            Videos.Clear();
            int currentProgress = 1;
            progress.Report(currentProgress);

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
                    using HttpClient client = new();
                    dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync($"https://noembed.com/embed?url={link}"));
                    string html = json.html;
                    HtmlDocument doc = new();
                    doc.LoadHtml(html);

                    string tweetAuthor = ((string)json.author_url).Split("/").Last();
                    string tweetContent = $"@{tweetAuthor} - {doc.DocumentNode.SelectNodes("//text()")?.First().InnerText}";

                    string authorImage = $"https://unavatar.io/twitter/{tweetAuthor}";

                    //string userImage = results[0][1].user.profile_image;
                    //results[0][1].content;
                    //tweetContent = tweetContent.Replace("\n", "").Replace("\r", "");

                    Videos.Add(new Video { Title = tweetContent, Link = link, Thumbnail = authorImage, Site = "Twitter" });
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
            Mouse.OverrideCursor = null;
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
            downloader.StartInfo.FileName = Config.Settings.YoutubeDL;
            downloader.StartInfo.Arguments += $" {Config.Settings.AdditionalArgs} ";
            downloader.StartInfo.Arguments += "--newline ";

            if (video.Site == "YouTube") {
                // Video
                if (Config.Settings.ExportType == 0) {
                    // Original
                    if (Config.Settings.ExportFormatVideo == 0)
                        downloader.StartInfo.Arguments += $"--format bestvideo+bestaudio {video.Link} -o \"{Config.Settings.OutDir}\\%(title)s.%(ext)s\"";

                    // MP4
                    else if (Config.Settings.ExportFormatVideo == 1)
                        downloader.StartInfo.Arguments += $"--format \"bestvideo+bestaudio[ext=m4a]/bestvideo+bestaudio/best\" --merge-output-format mp4 {video.Link} -o \"{Config.Settings.OutDir}\\%(title)s.%(ext)s\"";
                }
                //Audio
                else if (Config.Settings.ExportType == 1) {
                    // FLAC
                    if (Config.Settings.ExportFormatVideo == 0)
                        downloader.StartInfo.Arguments += $"-f bestaudio -x --audio-format flac {video.Link} -o \"{Config.Settings.OutDir}\\%(title)s.%(ext)s\"";

                    // MP3
                    else if (Config.Settings.ExportFormatVideo == 1)
                        downloader.StartInfo.Arguments += $"-f bestaudio -x --audio-format mp3 {video.Link} -o \"{Config.Settings.OutDir}\\%(title)s.%(ext)s\"";
                }
            }

            else if (video.Site == "Twitter") {
                downloader.StartInfo.Arguments = $"{video.Link} -o \"{Config.Settings.OutDir}\\%(title)s.%(ext)s\"";
            }

            else if (video.Site == "Reddit") {
                downloader.StartInfo.Arguments = $"{video.Link} -o \"{Config.Settings.OutDir}\\%(title)s.%(ext)s\"";
            }

            if (!Config.Settings.ShowWindows) downloader.StartInfo.CreateNoWindow = true;
            downloader.Start();
            downloader.WaitForExit();
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e) {
            Video video = VideoListBox.SelectedItem as Video;
            SaveFileDialog saveFileDialog = new() { InitialDirectory = Config.Settings.OutDir, Filter = "Image|*.*" };
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

        private async void DownloadAllButton_Click(object sender, RoutedEventArgs e) {
            Mouse.OverrideCursor = Cursors.Wait;

            IProgress<int> progress = new Progress<int>(p => {
                ProgressBar.Value = p;
                ProgressBar.Maximum = Videos.Count;
            });

            int currentProgress = 1;
            progress.Report(currentProgress);
            ProgressBar.Visibility = Visibility.Visible;
            await Task.Run(async () => {
                Parallel.ForEach(Videos, video => {
                    Download(video);
                    progress.Report(currentProgress++);
                });
                await Task.Delay(200);
            });
            ProgressBar.Visibility = Visibility.Collapsed;

            Mouse.OverrideCursor = null;

            Process.Start(new ProcessStartInfo(Config.Settings.OutDir) { UseShellExecute = true });
        }

        private async void ListBoxDownloadVideo_Click(object sender, RoutedEventArgs e) {
            Video video = ((Button)sender).DataContext as Video;

            Mouse.OverrideCursor = Cursors.Wait;

            IProgress<int> progress = new Progress<int>(p => {
                ProgressBar.Value = p;
                ProgressBar.Maximum = 12;
            });

            progress.Report(1); 
            ProgressBar.Visibility = Visibility.Visible;
            await Task.Run(async () => {
                Download(video);
                progress.Report(12);
                await Task.Delay(200);
            });
            ProgressBar.Visibility = Visibility.Collapsed;

            Mouse.OverrideCursor = null;

            Process.Start(new ProcessStartInfo(Config.Settings.OutDir) { UseShellExecute = true });
        }

        private void ExtensionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            Config.Save();

            if (Config.Settings.ExportType == 0) {
                FormatVideoComboBox.Visibility = Visibility.Visible;
                FormatAudioComboBox.Visibility = Visibility.Collapsed;
            }
            else if (Config.Settings.ExportType == 1) {
                FormatVideoComboBox.Visibility = Visibility.Collapsed;
                FormatAudioComboBox.Visibility = Visibility.Visible;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);
            if (e.Key == Key.Return)
                FocusManager.SetFocusedElement(this, this);
        }
    }
}
