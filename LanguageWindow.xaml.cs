using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace BazaChecker
{
    public partial class LanguageWindow : Window
    {
        public string SelectedLanguage { get; private set; } = "ru";

        public LanguageWindow()
        {
            InitializeComponent();
            UpdateSelection();
        }

        public LanguageWindow(string currentLanguage) : this()
        {
            SelectedLanguage = currentLanguage;
            UpdateSelection();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void UpdateSelection()
        {
            var purpleBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A855F7"));
            var grayBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A35"));
            var darkGrayBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B3B4F"));
            var transparentBrush = Brushes.Transparent;
            
            // English selection
            EnglishBorder.BorderBrush = SelectedLanguage == "en" ? purpleBrush : grayBrush;
            EnglishBorder.Effect = SelectedLanguage == "en" 
                ? new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString("#A855F7"), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.3 }
                : null;
            EnglishCircle.BorderBrush = SelectedLanguage == "en" ? purpleBrush : darkGrayBrush;
            EnglishCircle.Background = SelectedLanguage == "en" ? purpleBrush : transparentBrush;
            EnglishCheckmark.Visibility = SelectedLanguage == "en" ? Visibility.Visible : Visibility.Collapsed;
            
            // Russian selection
            RussianBorder.BorderBrush = SelectedLanguage == "ru" ? purpleBrush : grayBrush;
            RussianBorder.Effect = SelectedLanguage == "ru" 
                ? new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString("#A855F7"), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.3 }
                : null;
            RussianCircle.BorderBrush = SelectedLanguage == "ru" ? purpleBrush : darkGrayBrush;
            RussianCircle.Background = SelectedLanguage == "ru" ? purpleBrush : transparentBrush;
            RussianCheckmark.Visibility = SelectedLanguage == "ru" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void English_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedLanguage = "en";
            UpdateSelection();
        }

        private void Russian_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedLanguage = "ru";
            UpdateSelection();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
