using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using Newtonsoft.Json;
using HtmlAgilityPack;

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
            public string Playlist { get; set; }
        }
        public ObservableCollection<Video> Videos = new();

        public class Link {
            public string URL { get; set; }
            public string Playlist { get; set; }
        }

        public MainWindow() {
            InitializeComponent();

            Title += $" {App.Version}";

            LinkBox.LostFocus += (s, e) => LinkChanged();

            VideoListBox.ItemsSource = Videos;
            VideoListBox.SelectionChanged += (s, e) => UpdateUI();

            ExtensionComboBox.SelectionChanged += ExtensionComboBox_SelectionChanged; ;
            FormatAudioComboBox.SelectionChanged += (s, e) => Config.Save();
            FormatVideoComboBox.SelectionChanged += (s, e) => Config.Save();
            ExtensionComboBox.DataContext = Config.Settings;
            FormatAudioComboBox.DataContext = Config.Settings;
            FormatVideoComboBox.DataContext = Config.Settings;

            ImageThumbnail.MouseDown += (s, e) => Process.Start(new ProcessStartInfo((VideoListBox.SelectedItem as Video).Thumbnail) { UseShellExecute = true });

            MouseDown += (s, e) => FocusManager.SetFocusedElement(this, this);

            if (Config.Settings.PasteOnStartup) {
                string paste = Clipboard.GetText();
                if (paste.Contains("http")) {
                    LinkBox.Text = paste;
                    LinkChanged();
                }
            }
        }

        private async void LinkChanged() {
            string[] strings = String.Concat(LinkBox.Text.Where(c => !Char.IsWhiteSpace(c))).Split(",");

            List<Link> links = new();

            foreach (string link in strings.Where(s => Uri.IsWellFormedUriString(s, UriKind.Absolute))) {
                if (link.Contains("youtu") && link.Contains("playlist")) {
                    Mouse.OverrideCursor = Cursors.Wait;
                    ProcessStartInfo p = new() {
                        FileName = Config.Settings.UtilsDir + "yt-dlp.exe",
                        CreateNoWindow = true,
                        Arguments = $"-j --flat-playlist {link}",
                        RedirectStandardOutput = true,
                    };
                    Process process = Process.Start(p);
                    string output = process.StandardOutput.ReadToEnd();
                    output = $"[{output.Replace("\n", ",")}]";
                    output = output.Remove(output.LastIndexOf(","), 1);
                    dynamic[] json = JsonConvert.DeserializeObject<dynamic[]>(output);

                    using HttpClient client = new();
                    string playlistTitle = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync($"https://youtube.com/oembed?url={link}")).title;

                    foreach (dynamic item in json) {
                        links.Add(new Link { URL = (string)item.url, Playlist = playlistTitle });
                    }
                    Mouse.OverrideCursor = null;
                }
                else {
                    links.Add(new Link { URL = link });
                }
            }

            IProgress<int> progress = new Progress<int>(p => {
                ProgressBar.Value = p;
                ProgressBar.Maximum = links.Count;
            });

            if (links.Count > 5) ProgressBar.Visibility = Visibility.Visible;
            await GenerateList(links, progress);
            ProgressBar.Visibility = Visibility.Collapsed;

            if (Videos.Count > 0) VideoListBox.SelectedIndex = 0;

            UpdateUI();
        }

        private async Task GenerateList(List<Link> links, IProgress<int> progress) {
            Mouse.OverrideCursor = Cursors.Wait;
            Videos.Clear();
            int currentProgress = 1;
            progress.Report(currentProgress);

            foreach (Link link in links) {
                // YouTube
                if (link.URL.Contains("youtu")) {
                    // Check if restricted/unembeddable
                    try {
                        using HttpClient client = new();
                        dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync($"https://youtube.com/oembed?url={link.URL}"));
                        string videoTitle = json.title;
                        string videoID = ((string)json.thumbnail_url).Split("/")[^2];

                        string thumbnailURL = $"https://img.youtube.com/vi/{videoID}/maxresdefault.jpg";
                        if (!IsValidImage(thumbnailURL))
                            thumbnailURL = $"https://img.youtube.com/vi/{videoID}/mqdefault.jpg";

                        Videos.Add(new Video { Title = videoTitle, Link = link.URL, Thumbnail = thumbnailURL, Site = "YouTube", Playlist = link.Playlist });
                    }
                    catch {
                        if (link.URL.Contains("www.youtube.com/watch?v=")) {
                            string videoID = HttpUtility.ParseQueryString(new Uri(link.URL).Query).Get("v");
                            string videoTitle = $"Restricted YouTube Video (ID: {videoID})";
                            string thumbnailURL = $"https://img.youtube.com/vi/{videoID}/mqdefault.jpg";

                            if (!IsValidImage(thumbnailURL))
                                videoTitle = $"Unknown YouTube Video (ID: {videoID})";

                            Videos.Add(new Video { Title = videoTitle, Link = link.URL, Thumbnail = thumbnailURL, Site = "YouTube", Playlist = link.Playlist });
                        }
                        else if (link.URL.Contains("youtu.be/")) {
                            string videoID = link.URL.Split("/")[3];
                            string videoTitle = $"Restricted YouTube Video (ID: {videoID})";
                            string thumbnailURL = $"https://img.youtube.com/vi/{videoID}/mqdefault.jpg";

                            if (!IsValidImage(thumbnailURL))
                                videoTitle = $"Unknown YouTube Video (ID: {videoID})";

                            Videos.Add(new Video { Title = videoTitle, Link = link.URL, Thumbnail = thumbnailURL, Site = "YouTube", Playlist = link.Playlist });
                        }
                    }
                }
                // Twitter
                if (link.URL.Contains("twitter.com")) {
                    using HttpClient client = new();
                    dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync($"https://noembed.com/embed?url={link.URL}"));
                    string html = json.html;
                    HtmlDocument doc = new();
                    doc.LoadHtml(html);

                    string tweetAuthor = ((string)json.author_url).Split("/").Last();
                    string tweetContent = $"@{tweetAuthor} - {doc.DocumentNode.SelectNodes("//text()")?.First().InnerText}";
                    string authorImage = $"https://unavatar.io/twitter/{tweetAuthor}";

                    Videos.Add(new Video { Title = tweetContent, Link = link.URL, Thumbnail = authorImage, Site = "Twitter" });
                }
                // Reddit
                if (link.URL.Contains("reddit.com")) {
                    using HttpClient client = new();
                    dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync($"{link.URL}.json"));
                    string videoTitle = json[0].data.children[0].data.title;
                    string videoThumbnail = json[0].data.children[0].data.thumbnail;

                    Videos.Add(new Video { Title = videoTitle, Link = link.URL, Thumbnail = videoThumbnail, Site = "Reddit" });
                }
                // Instagram
                if (link.URL.Contains("instagram.com")) {
                    string videoTitle = $"Instagram Video ({new Uri(link.URL).AbsolutePath[1..^1]})";
                    string videoThumbnail = "https://upload.wikimedia.org/wikipedia/commons/thumb/9/95/Instagram_logo_2022.svg/600px-Instagram_logo_2022.svg.png";

                    Videos.Add(new Video { Title = videoTitle, Link = link.URL, Thumbnail = videoThumbnail, Site = "Instagram" });
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

            if (Videos.Count > 0)
                DownloadAllButton.IsEnabled = true;
            else
                DownloadAllButton.IsEnabled = false;
        }

        private void Download(Video video) {
            string outDir = Config.Settings.OutDir;
            if (!String.IsNullOrEmpty(video.Playlist)) outDir += $"\\{video.Playlist}\\";

            using Process downloader = new();
            downloader.StartInfo.FileName = Config.Settings.UtilsDir + "yt-dlp.exe";
            downloader.StartInfo.Arguments += $" {Config.Settings.AdditionalArgs} ";

            downloader.EnableRaisingEvents = true;
            downloader.StartInfo.Arguments += " --newline ";

            if (video.Site == "YouTube") {
                // Check for cookies.txt
                if (File.Exists(Path.Combine(Config.Settings.UtilsDir, "cookies.txt"))) 
                    downloader.StartInfo.Arguments += $" --cookies \"{Path.Combine(Config.Settings.UtilsDir, "cookies.txt")}\" ";

                // Video
                if (Config.Settings.ExportType == 0) {
                    // Original
                    if (Config.Settings.ExportFormatVideo == 0)
                        downloader.StartInfo.Arguments += $"--format bestvideo+bestaudio {video.Link} -o \"{outDir}\\%(title)s.%(ext)s\"";

                    // MP4
                    else if (Config.Settings.ExportFormatVideo == 1)
                        downloader.StartInfo.Arguments += $"--format \"bestvideo+bestaudio[ext=m4a]/bestvideo+bestaudio/best\" --merge-output-format mp4 {video.Link} -o \"{outDir}\\%(title)s.%(ext)s\"";
                }
                //Audio
                else if (Config.Settings.ExportType == 1) {
                    // FLAC
                    if (Config.Settings.ExportFormatVideo == 0)
                        downloader.StartInfo.Arguments += $"-f bestaudio -x --audio-format flac {video.Link} -o \"{outDir}\\%(title)s.%(ext)s\"";

                    // MP3
                    else if (Config.Settings.ExportFormatVideo == 1)
                        downloader.StartInfo.Arguments += $"-f bestaudio -x --audio-format mp3 {video.Link} -o \"{outDir}\\%(title)s.%(ext)s\"";
                }
            }

            else if (video.Site == "Twitter") {
                downloader.StartInfo.Arguments = $"{video.Link} -o \"{Config.Settings.OutDir}\\%(title)s.%(ext)s\"";
            }

            else if (video.Site == "Reddit") {
                downloader.StartInfo.Arguments = $"{video.Link} -o \"{Config.Settings.OutDir}\\%(title)s.%(ext)s\"";
            }

            else if (video.Site == "Instagram") {
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
            if (Videos.Count == 1) {
                ListBoxDownloadVideo_Click(Videos[0], e);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;

            IProgress<int> progress = new Progress<int>(p => {
                ProgressBar.Value = p;
                ProgressBar.Maximum = Videos.Count;
                TaskbarItemInfo.ProgressValue = (double)p / Videos.Count;
            });

            int currentProgress = 1;
            progress.Report(currentProgress);
            ProgressBar.Visibility = Visibility.Visible;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            await Task.Run(async () => {
                Parallel.ForEach(Videos, new ParallelOptions { MaxDegreeOfParallelism = Config.Settings.MaxConcurrentDownloads }, video => {
                    Download(video);
                    progress.Report(currentProgress++);
                });
                await Task.Delay(200);
            });
            ProgressBar.Visibility = Visibility.Collapsed;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

            Mouse.OverrideCursor = null;
            SystemSounds.Exclamation.Play();

            Process.Start(new ProcessStartInfo(Config.Settings.OutDir) { UseShellExecute = true });
        }

        private async void ListBoxDownloadVideo_Click(object sender, RoutedEventArgs e) {
            Video video;
            if (sender is Video) 
                video = sender as Video;
            else 
                video = ((Button)sender).DataContext as Video;

            Mouse.OverrideCursor = Cursors.Wait;


            IProgress<int> progress = new Progress<int>(p => {
                ProgressBar.Value = p;
                ProgressBar.Maximum = 12;
                TaskbarItemInfo.ProgressValue = (double)p / 12;
            });

            progress.Report(1); 
            ProgressBar.Visibility = Visibility.Visible;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            await Task.Run(async () => {
                Download(video);
                progress.Report(12);
                await Task.Delay(200);
            });
            ProgressBar.Visibility = Visibility.Collapsed;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

            Mouse.OverrideCursor = null;
            SystemSounds.Exclamation.Play();

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
        public bool IsValidImage(string url) {
            HttpClient client = new();
            using HttpResponseMessage response = client.Send(new(HttpMethod.Head, new Uri(url)));
            return response.StatusCode == HttpStatusCode.OK;
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);
            if (e.Key == Key.Return)
                FocusManager.SetFocusedElement(this, this);
            if (e.Key == Key.F12)
                Process.Start(new ProcessStartInfo(Config.Settings.OutDir) { UseShellExecute = true });
        }
    }
}
