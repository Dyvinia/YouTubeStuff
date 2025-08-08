using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeStuff.Utils.Format {
    public class VideoFormat {
        public string Id { get; set; }

        public string Format { get; set; }
        public string FormatText {
            get {
                string format = Format.Split('(')[0].Trim();
                if (Format.Contains("Premium"))
                    format += " (Premium)";
                return format;
            }
        }

        public string Extension { get; set; }
        public string Codec { get; set; }
        public string CodecClean {
            get {
                if (string.IsNullOrEmpty(Codec))
                    return "Unknown Codec";
                return Codec.Split('.')[0]
                    .Replace("avc1", "H.264")
                    .Replace("mp4a", "AAC")
                    .Replace("opus", "Opus")
                    .Replace("vp9", "VP9")
                    .Replace("vp09", "VP9")
                    .Replace("av01", "AV1");
            } 
        }

        public int Filesize { get; set; }
        public string FilesizeText {
            get {
                if (Filesize > 0) {
                    if (Filesize < 1024)
                        return $"{Filesize} B";
                    else if (Filesize < 1024 * 1024)
                        return $"{Filesize / 1024.0:0.00}KB";
                    else if (Filesize < 1024 * 1024 * 1024)
                        return $"{Filesize / (1024.0 * 1024.0):0.00}MB";
                    else
                        return $"{Filesize / (1024.0 * 1024.0 * 1024.0):0.00}GB";
                }
                return "Unknown Size";
            }
        }

        public override string ToString() {
            return $"{Id} - {Format} [{CodecClean}] {FilesizeText}";
        }
    }
}
