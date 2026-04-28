using System.Collections.Specialized;
using System.Windows;
using AquaVectorUI.viewmodel;

namespace AquaVectorUI.views
{
    public partial class ReceiveLogControl : System.Windows.Controls.UserControl
    {
        public ReceiveLogControl() => InitializeComponent();

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            vm.Logs.CollectionChanged += Logs_CollectionChanged;
        }

        private void Logs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[^1]);
        }
    }
}
