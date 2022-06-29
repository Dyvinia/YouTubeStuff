using Microsoft.Win32;
using System;
using System.Collections.Generic;
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

        public string ThumbnailURL;

        public MainWindow() {
            InitializeComponent();

            MouseDown += (s, e) => FocusManager.SetFocusedElement(this, this);
        }

        private void LinkBox_TextChanged(object sender, TextChangedEventArgs e) {
            string text = (sender as TextBox).Text;

            if (Uri.IsWellFormedUriString(text, UriKind.Absolute)) {
                Uri uri = new(text);

                if (text.Contains("www.youtube.com/watch?v=")) {
                    string videoID = HttpUtility.ParseQueryString(uri.Query).Get("v");


                    ThumbnailURL = $"https://img.youtube.com/vi/{videoID}/maxresdefault.jpg";
                }
                else if (text.Contains("youtu.be/")) {
                    string videoID = text.Split("/").Last();

                    ThumbnailURL = $"https://img.youtube.com/vi/{videoID}/maxresdefault.jpg";
                    
                }

                ImageThumbnail.Source = new BitmapImage(new Uri(ThumbnailURL));
            }
            else {
                ImageThumbnail.Source = null;
            }

            if (String.IsNullOrEmpty(ThumbnailURL)) {
                ButtonClipboard.IsEnabled = false;
                ButtonSave.IsEnabled = false;
            }
            else {
                ButtonClipboard.IsEnabled = true;
                ButtonSave.IsEnabled = true;
            }
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e) {
            SaveFileDialog saveFileDialog = new() { InitialDirectory = App.OutDir, Filter = "Image|*.*" };
            if (saveFileDialog.ShowDialog() == true) {
                using HttpClient httpClient = new();
                string fileExtension = Path.GetExtension(ThumbnailURL);
                string finalPath = $"{saveFileDialog.FileName}{fileExtension}";

                byte[] imageBytes = await httpClient.GetByteArrayAsync(new Uri(ThumbnailURL));
                await File.WriteAllBytesAsync(finalPath, imageBytes);
            }
        }

        private void ButtonClipboard_Click(object sender, RoutedEventArgs e) {
            IDataObject data = new DataObject();
            data.SetData(DataFormats.Bitmap, new BitmapImage(new Uri(ThumbnailURL)), true);
            Clipboard.SetDataObject(data, true);
        }
    }
}
