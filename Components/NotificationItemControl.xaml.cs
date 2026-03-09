using System;
using System.Windows;
using System.Windows.Controls;

namespace BazaChecker.Components
{
    /// <summary>
    /// Notification item UserControl
    /// </summary>
    public partial class NotificationItemControl : UserControl
    {
        /// <summary>
        /// Event raised when close button is clicked
        /// </summary>
        public event EventHandler<int>? CloseRequested;

        public NotificationItemControl()
        {
            InitializeComponent();
        }

        private void CloseNotification_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                CloseRequested?.Invoke(this, id);
            }
            else if (sender is Button btn2 && int.TryParse(btn2.Tag?.ToString(), out int parsedId))
            {
                CloseRequested?.Invoke(this, parsedId);
            }
        }
    }
}
