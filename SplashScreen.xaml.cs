using System.Windows;

namespace BazaChecker
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
        }

        public void UpdateStatus(string status)
        {
            TxtStatus.Text = status;
        }

        public void UpdateProgress(double percent)
        {
            ProgressFill.Width = (percent / 100.0) * 300; // 300 is the progress bar width
        }
    }
}
