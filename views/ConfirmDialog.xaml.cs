using System.Windows;

namespace AquaVectorUI.views
{
    public partial class ConfirmDialog : Window
    {
        public bool Confirmed { get; private set; }

        private ConfirmDialog(string title, string message)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
        }

        // Position at top-right of the main window, away from the center map.
        protected override void OnContentRendered(System.EventArgs e)
        {
            base.OnContentRendered(e);
            var main = Application.Current.MainWindow;
            if (main != null && main != this)
            {
                Left = main.Left + main.ActualWidth - ActualWidth - 20;
                Top  = main.Top + 50;
            }
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }

        public static bool Show(string title, string message)
        {
            var dlg = new ConfirmDialog(title, message)
            {
                Owner = Application.Current.MainWindow
            };
            dlg.ShowDialog();
            return dlg.Confirmed;
        }
    }
}
