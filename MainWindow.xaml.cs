using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            public string Thumbnail { get; set; }
        }
        public ObservableCollection<Video> Videos = new();

        public MainWindow() {
            InitializeComponent();

            VideoListBox.ItemsSource = Videos;
            VideoListBox.SelectionChanged += (s, e) => UpdateUI();

            MouseDown += (s, e) => FocusManager.SetFocusedElement(this, this);
        }

        private async void LinkBox_TextChanged(object sender, TextChangedEventArgs e) {
            string text = (sender as TextBox).Text;
            string[] strings = String.Concat(text.Where(c => !Char.IsWhiteSpace(c))).Split(",");
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
                // Youtube
                if (link.Contains("youtu")) {
                    using HttpClient client = new();
                    dynamic json = JsonConvert.DeserializeObject<dynamic>(await client.GetStringAsync($"https://noembed.com/embed?url={link}"));
                    string videoTitle = json.title;
                    string videoID = ((string)json.thumbnail_url).Split("/")[^2];

                    Videos.Add(new Video { Title = videoTitle, Thumbnail = $"https://img.youtube.com/vi/{videoID}/maxresdefault.jpg" });
                }

                progress.Report(currentProgress++);
            }
        }

        private void UpdateUI() {
            if (VideoListBox.SelectedItem is not Video video) return;

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
    }
}
