using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace BazaChecker
{
    public partial class VmReportWindow : Window
    {
        public VmReportWindow(string log)
        {
            InitializeComponent();
            FormatLog(log);
        }

        private void FormatLog(string log)
        {
            var paragraph = new Paragraph();
            var lines = log.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (line.Contains("DETECTED"))
                {
                    // Bold red for detected lines
                    var run = new Run(line + "\n")
                    {
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
                        FontWeight = FontWeights.Bold
                    };
                    paragraph.Inlines.Add(run);
                }
                else if (line.Contains("[!] RESULT"))
                {
                    // Bold yellow for result line
                    var run = new Run(line + "\n")
                    {
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24")),
                        FontWeight = FontWeights.Bold
                    };
                    paragraph.Inlines.Add(run);
                }
                else if (line.Contains("[*] RESULT") || line.Contains("CLEAN"))
                {
                    // Bold green for clean
                    var run = new Run(line + "\n")
                    {
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E")),
                        FontWeight = FontWeights.Bold
                    };
                    paragraph.Inlines.Add(run);
                }
                else if (line.StartsWith("[-]") || line.StartsWith("--"))
                {
                    // Gray for headers/separators
                    var run = new Run(line + "\n")
                    {
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"))
                    };
                    paragraph.Inlines.Add(run);
                }
                else
                {
                    // Normal text
                    var run = new Run(line + "\n")
                    {
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB"))
                    };
                    paragraph.Inlines.Add(run);
                }
            }

            ConsoleOutput.Document = new FlowDocument(paragraph);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
