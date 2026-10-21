using System;
using System.ComponentModel;

namespace Digits {
    // Based on: https://docs.yubico.com/yesdk/users-manual/application-oath/uri-string-format.html
    [System.Runtime.Serialization.DataContract]
    public class DatabaseElement : INotifyPropertyChanged {
        private string _iconPath;

        [System.Runtime.Serialization.DataMember(Order = 1)]
        public int Id { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 2)]
        public string Type { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 3)]
        public string Label { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [System.Runtime.Serialization.IgnoreDataMember]
        [System.Xml.Serialization.XmlIgnore]
        public string IconPath {
            get {
                return _iconPath;
            }
            set {
                if (string.Equals(_iconPath, value, StringComparison.Ordinal)) {
                    return;
                }

                _iconPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconPath)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconGlyphVisibility)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [System.Runtime.Serialization.IgnoreDataMember]
        [System.Xml.Serialization.XmlIgnore]
        public Windows.UI.Xaml.Visibility IconGlyphVisibility {
            get {
                return string.IsNullOrWhiteSpace(IconPath) ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [System.Runtime.Serialization.DataMember(Order = 4)]
        public string Secret { get; set; }

        [System.Runtime.Serialization.IgnoreDataMember]
        [System.Xml.Serialization.XmlIgnore]
        public string ProtectedSecret { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 5)]
        public string Issuer { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 6)]
        public string Algorithm { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 7)]
        public int Digits { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 8)]
        public int Counter { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 9)]
        public int Period { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [System.Runtime.Serialization.IgnoreDataMember]
        [System.Xml.Serialization.XmlIgnore]
        public string IconGlyph {
            get {
                string source = !string.IsNullOrWhiteSpace(Issuer) ? Issuer.Trim() : Label?.Trim();
                if (string.IsNullOrWhiteSpace(source)) {
                    return "?";
                }

                var words = source.Split(new[] { ' ', '-', '_', '.', '@' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 1) {
                    return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();
                }

                return source.Substring(0, 1).ToUpperInvariant();
            }
        }
    }
}
