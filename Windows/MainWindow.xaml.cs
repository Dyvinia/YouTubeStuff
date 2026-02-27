using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Microsoft.Win32;
using Newtonsoft.Json;
using YouTubeStuff.Utils.Format;

namespace YouTubeStuff {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public class Link {
            public string URL { get; set; }
            public string Playlist { get; set; }
        }

        public ObservableCollection<Video> Videos = [];

        public MainWindow() {
            InitializeComponent();

            Title += $" {App.Version}";

            LinkBox.LostFocus += (s, e) => LinkChanged();

            VideoListBox.ItemsSource = Videos;
            VideoListBox.SelectionChanged += (s, e) => UpdateUI();

            ExtensionComboBox.SelectionChanged += ExtensionComboBox_SelectionChanged; ;
            FormatAudioComboBox.SelectionChanged += (s, e) => Config.Save();
            FormatVideoComboBox.SelectionChanged += (s, e) => Config.Save();
            SubtitlesComboBox.SelectionChanged += (s, e) => Config.Save();
            ExtensionComboBox.DataContext = Config.Settings;
            FormatAudioComboBox.DataContext = Config.Settings;
            FormatVideoComboBox.DataContext = Config.Settings;
            SubtitlesComboBox.DataContext = Config.Settings;

            ImageThumbnail.MouseDown += (s, e) => Process.Start(new ProcessStartInfo((VideoListBox.SelectedItem as Video).Thumbnail) { UseShellExecute = true });

            MouseDown += (s, e) => FocusManager.SetFocusedElement(this, this);

            if (Config.Settings.PasteOnStartup) {
                string paste = Clipboard.GetText();
                if (paste.Contains("http")) {
                    Dispatcher.BeginInvoke(() => {
                        LinkBox.Text = paste;
                        LinkChanged();
                    });
                }
            }

            Config.Save();
        }

        private async void LinkChanged() {
            List<Link> links = [];

            // Different separators
            char[] separators = [',', ' ', ';'];

            foreach (string link in LinkBox.Text.Split(separators).Where(s => Uri.IsWellFormedUriString(s, UriKind.Absolute))) {
                // youtube playlist
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
                    output = output.Remove(output.LastIndexOf(','), 1);
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
                ProgressBar.Maximum = links.Count + 1;
                TaskbarItemInfo.ProgressValue = p / (links.Count + 1d);
            });

            ProgressBar.Visibility = Visibility.Visible;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            await GenerateList(links, progress);
            ProgressBar.Visibility = Visibility.Collapsed;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

            if (Videos.Count > 0) VideoListBox.SelectedIndex = 0;

            UpdateUI();
        }

        private async Task GenerateList(List<Link> links, IProgress<int> progress) {
            Mouse.OverrideCursor = Cursors.Wait;
            Videos.Clear();
            progress.Report(1);

            ConcurrentDictionary<int, Link> orderedLinks = new(Enumerable.Range(0, links.Count).ToDictionary(i => i + 1, i => links[i]));
            ConcurrentDictionary<int, Video> orderedVideos = new();

            await Parallel.ForEachAsync(orderedLinks, async (indexedLink, _) => {
                int index = indexedLink.Key;
                Link link = indexedLink.Value;

                Process ytdlpJson = new() {
                    StartInfo = new() {
                        FileName = Path.Combine(Config.Settings.UtilsDir, "yt-dlp.exe"),
                        Arguments = $"-J {link.URL}",
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                    }
                };
                ytdlpJson.Start();
                string output = ytdlpJson.StandardOutput.ReadToEnd();
                JsonElement rootElement = JsonDocument.Parse(output).RootElement;

                // YouTube
                if (link.URL.Contains("youtu")) {
                    string videoTitle = rootElement.GetProperty("title").GetString();
                    string videoID = rootElement.GetProperty("id").GetString();
                    string thumbnailURL = rootElement.GetProperty("thumbnail").GetString();
                    rootElement.TryGetProperty("duration", out JsonElement duration);

                    rootElement.TryGetProperty("formats", out JsonElement formats);
                    List<VideoFormat> videoFormats = [];
                    foreach (JsonElement format in formats.EnumerateArray().Where(f => (f.TryGetProperty("video_ext", out JsonElement formatExt) && formatExt.GetString() != "none"))) {
                        format.TryGetProperty("filesize", out JsonElement fileSizeProp);

                        int fileSize;
                        if (fileSizeProp.ValueKind == JsonValueKind.Null || fileSizeProp.ValueKind == JsonValueKind.Undefined)
                            fileSize = (int)(format.GetProperty("tbr").GetDouble() * 125 * duration.GetDouble()) * -1; // too lazy to properly add approx prefix
                        else
                            fileSize = fileSizeProp.GetInt32();

                        videoFormats.Add(new() {
                            Id = format.GetProperty("format_id").GetString(),
                            Format = format.GetProperty("format").GetString().Split('-')[1].Trim(),
                            Extension = format.GetProperty("ext").GetString(),
                            Codec = format.GetProperty("vcodec").GetString(),
                            Filesize = fileSize
                        });
                    }
                    videoFormats.Reverse();

                    orderedVideos.TryAdd(index, new() { Title = videoTitle, Link = link.URL, Thumbnail = thumbnailURL, Duration = duration.GetDouble(), Site = "YouTube", Playlist = link.Playlist, Formats = videoFormats });
                }

                // Twitter
                if (link.URL.Contains("twitter.com") || link.URL.Contains("//x.com")) {
                    try {
                        string videoTitle = rootElement.GetProperty("title").GetString();
                        string thumbnailURL = rootElement.GetProperty("entries")[0].GetProperty("thumbnail").GetString();
                        rootElement.TryGetProperty("duration", out JsonElement duration);

                        orderedVideos.TryAdd(index, new() { Title = videoTitle, Link = link.URL, Thumbnail = thumbnailURL, Duration = duration.GetDouble(), Site = "Twitter" });
                    }
                    catch {
                        try {
                            // Get json from fxtwitter api
                            string apiLink;
                            if (link.URL.Contains("fxtwitter.com"))
                                apiLink = link.URL.Replace("fxtwitter.com", "api.fxtwitter.com");
                            else if (link.URL.Contains("vxtwitter.com"))
                                apiLink = link.URL.Replace("vxtwitter.com", "api.fxtwitter.com");
                            else if (link.URL.Contains("//x.com"))
                                apiLink = link.URL.Replace("//x.com", "//api.fxtwitter.com");
                            else
                                apiLink = link.URL.Replace("//twitter.com", "//api.fxtwitter.com");

                            using HttpClient client = new();
                            dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync(apiLink, CancellationToken.None));

                            string tweetAuthor = json.tweet.author.name;
                            string tweetContent = json.tweet.text;
                            string thumbnailURL = json.tweet.media.videos[0].thumbnail_url;

                            orderedVideos.TryAdd(index, new() { Title = $"{tweetAuthor} - {tweetContent}", Link = link.URL, Thumbnail = thumbnailURL, Site = "Twitter" });
                        }
                        catch {
                            string videoTitle = rootElement.GetProperty("title").GetString();

                            orderedVideos.TryAdd(index, new() { Title = videoTitle, Link = link.URL, Thumbnail = "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6f/Logo_of_Twitter.svg/512px-Logo_of_Twitter.svg.png", Site = "Twitter" });
                        }
                    }
                }

                // Reddit
                if (link.URL.Contains("reddit.com")) {
                    using HttpClient client = new();
                    dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync($"{link.URL}.json", CancellationToken.None));
                    string videoTitle = json[0].data.children[0].data.title;
                    string videoThumbnail = json[0].data.children[0].data.thumbnail;

                    orderedVideos.TryAdd(index, new() { Title = videoTitle, Link = link.URL, Thumbnail = videoThumbnail, Site = "Reddit" });
                }

                // Instagram
                if (link.URL.Contains("instagram.com")) {
                    try {
                        string videoTitle = rootElement.GetProperty("title").GetString();
                        string thumbnailURL = rootElement.GetProperty("thumbnail").GetString();
                        rootElement.TryGetProperty("duration", out JsonElement duration);

                        orderedVideos.TryAdd(index, new() { Title = videoTitle, Link = link.URL, Thumbnail = thumbnailURL, Duration = duration.GetDouble(), Site = "Instagram" });
                    }
                    catch {
                        string videoTitle = $"Instagram Video ({new Uri(link.URL).AbsolutePath[1..^1]})";
                        string videoThumbnail = "https://upload.wikimedia.org/wikipedia/commons/thumb/9/95/Instagram_logo_2022.svg/600px-Instagram_logo_2022.svg.png";

                        orderedVideos.TryAdd(index, new() { Title = videoTitle, Link = link.URL, Thumbnail = videoThumbnail, Site = "Instagram" });
                    }
                }

                progress.Report(orderedVideos.Count);
            });

            foreach (Video video in orderedVideos.Values) {
                Videos.Add(video);
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

        private static void Download(Video video, IProgress<int> progress = null) {
            string outDir = Config.Settings.OutDir;
            if (!String.IsNullOrEmpty(video.Playlist)) outDir += $"\\{video.Playlist}\\";
            string output = $"{outDir}\\%(title)s.%(ext)s";

            using Process ytdl = new();
            ytdl.StartInfo.WorkingDirectory = Config.Settings.UtilsDir;

            if (Config.Settings.ShowWindows) {
                ytdl.StartInfo.FileName = "cmd.exe";
                ytdl.StartInfo.Arguments += $"/K {Config.Settings.UtilsDir + "yt-dlp.exe"} {Config.Settings.AdditionalArgs} ";
            }
            else {
                ytdl.StartInfo.FileName = Config.Settings.UtilsDir + "yt-dlp.exe";
                ytdl.StartInfo.Arguments += $"{Config.Settings.AdditionalArgs} ";
                ytdl.StartInfo.CreateNoWindow = true;
                ytdl.StartInfo.RedirectStandardOutput = true;
                ytdl.StartInfo.RedirectStandardError = true;
                ytdl.EnableRaisingEvents = true;
            }

            // Set Start time and End time
            if (video.StartTime != null || video.EndTime != null) {
                output = $"{outDir}\\%(title)s.cut.%(ext)s";
                ytdl.StartInfo.Arguments += $" --download-sections \"*{video.StartTime ?? "00:00"}-{video.EndTime ?? "inf"}\" ";
            }
            ytdl.StartInfo.Arguments += " --replace-in-metadata \"title\" \"[ &|:<>?*]\" \"_\" --replace-in-metadata \"title\" \"[^\\x00-\\x7F]\" \"\" ";

            switch (video.Site) {
                case "YouTube":
                    // Check for cookies.txt
                    if (File.Exists(Path.Combine(Config.Settings.UtilsDir, "cookies.txt")))
                        ytdl.StartInfo.Arguments += $" --cookies \"{Path.Combine(Config.Settings.UtilsDir, "cookies.txt")}\" ";

                    // Video
                    if (Config.Settings.ExportType == 0) {
                        string subs = string.Empty;
                        if (Config.Settings.Subtitles == 1) {
                            subs = "--sub-format srt --convert-subs srt --embed-subs";
                        }
                        else if (Config.Settings.Subtitles == 2) {
                            subs = $"--sub-format srt --convert-subs srt --embed-subs --exec \"ffmpeg -y -i \\\"{output}\\\" -vf \\\"subtitles='{output.Replace("\\", "\\\\\\\\").Replace(":", "\\:")}':si=0:force_style='Fontname=Roboto,Fontsize={Config.Settings.SubtitleFontSize}'\\\" -c:a copy \\\"{output}.subs.%(ext)s\\\"\"";
                        }

                        // Original
                        if (Config.Settings.ExportFormatVideo == 0)
                            ytdl.StartInfo.Arguments += $" --format {video.SelectedFormat.Id}+bestaudio {subs} {video.Link} -o \"{output}\"";

                        // MP4
                        else if (Config.Settings.ExportFormatVideo == 1)
                            ytdl.StartInfo.Arguments += $" --format \"{video.SelectedFormat.Id}+bestaudio[ext=m4a]/{video.SelectedFormat.Id}+bestaudio/best\" --merge-output-format mp4 --postprocessor-args \"-vcodec libx264 -acodec aac\" {subs} {video.Link} -o \"{output}\"";
                    }
                    //Audio
                    else if (Config.Settings.ExportType == 1) {
                        // Original
                        if (Config.Settings.ExportFormatAudio == 0)
                            ytdl.StartInfo.Arguments += $" -f bestaudio {video.Link} -o \"{output}\"";
                        
                        // FLAC
                        if (Config.Settings.ExportFormatAudio == 1)
                            ytdl.StartInfo.Arguments += $" -f bestaudio -x --audio-format flac {video.Link} -o \"{output}\"";

                        // WAV
                        else if (Config.Settings.ExportFormatAudio == 2)
                            ytdl.StartInfo.Arguments += $" -f bestaudio -x --audio-format wav {video.Link} -o \"{output}\"";

                        // MP3
                        else if (Config.Settings.ExportFormatAudio == 3)
                            ytdl.StartInfo.Arguments += $" -f bestaudio -x --audio-format mp3 {video.Link} -o \"{output}\"";
                    }
                    break;

                // Reddit, Twitter, Instagram
                default:
                    ytdl.StartInfo.Arguments = $"{video.Link} -o \"{output}\"";
                    break;
            }

            ytdl.Start();

            // cut video's download progress
            try {
                ytdl.BeginErrorReadLine();
                ytdl.ErrorDataReceived += (s, e) => {
                    string line = e.Data;
                    if (line?.Contains("time=") ?? false) {
                        string lineCutFront = line[(line.IndexOf("time=") + 5)..];
                        string linefinal = lineCutFront[..lineCutFront.IndexOf('b')];
                        if (Video.TryParseSeconds(linefinal.Trim(), out double currentSeconds))
                            progress?.Report((int)(currentSeconds * 100d / video.Duration));
                    }
                };
            }
            catch { }

            // video download progress
            string line = "";
            try {
                while (!ytdl.StandardOutput.EndOfStream) {
                    char character = (char)ytdl.StandardOutput.Read();
                    line += character;
                    Console.Write(character);
                    if (character == '\n')
                        line = "";
                    if (character == '\r') {
                        if (line.Contains("% of")) {
                            string lineCutFront = line[(line.IndexOf(']') + 1)..];
                            string linefinal = lineCutFront[..lineCutFront.IndexOf('%')];
                            if (double.TryParse(linefinal.Trim(), out double currentPercent))
                                progress?.Report((int)currentPercent);
                        }
                        line = "";
                    }
                }
            }
            catch { }

            ytdl.WaitForExit();
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
            DataObject data = new();
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
                ProgressBar.Maximum = 100;
                TaskbarItemInfo.ProgressValue = p / 100d;
            });

            progress.Report(2);
            ProgressBar.Visibility = Visibility.Visible;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            await Task.Run(async () => {
                Download(video, progress);
                progress.Report(100);
                await Task.Delay(100);
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
                SubtitlesComboBox.Visibility = Visibility.Visible;
            }
            else if (Config.Settings.ExportType == 1) {
                FormatVideoComboBox.Visibility = Visibility.Collapsed;
                FormatAudioComboBox.Visibility = Visibility.Visible;
                SubtitlesComboBox.Visibility = Visibility.Collapsed;
            }
        }


        [GeneratedRegex("[^0-9:]+")]
        private static partial Regex TimeRegex();
        private void LiveValidationTextBox(object sender, TextCompositionEventArgs e) => e.Handled = TimeRegex().IsMatch(e.Text);

        private void FinalValidationTextBox(object sender, RoutedEventArgs e) {
            TextBox text = sender as TextBox;
            if (text.Text.Length == 1) text.Text = $"0{text.Text}";
            if (text.Text.EndsWith(':')) text.Text = $"{text.Text}00";
            if (TimeSpan.TryParseExact(text.Text, @"ss", System.Globalization.CultureInfo.CurrentCulture, out _))
                text.Text = $"00:{text.Text}";
            else if (TimeSpan.TryParseExact(text.Text, @"m\:ss", System.Globalization.CultureInfo.CurrentCulture, out _) && !TimeSpan.TryParseExact(text.Text, @"mm\:ss", System.Globalization.CultureInfo.CurrentCulture, out _))
                text.Text = $"0{text.Text}";

            if (!TimeSpan.TryParseExact(text.Text, @"mm\:ss", System.Globalization.CultureInfo.CurrentCulture, out _)
                && !TimeSpan.TryParseExact(text.Text, @"h\:mm\:ss", System.Globalization.CultureInfo.CurrentCulture, out _)
                && !TimeSpan.TryParseExact(text.Text, @"hh\:mm\:ss", System.Globalization.CultureInfo.CurrentCulture, out _))
                text.Text = null;
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
