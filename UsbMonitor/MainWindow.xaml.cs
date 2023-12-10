using Microsoft.Toolkit.Uwp.Notifications;
using System.IO;
using System.Windows;
using System.Windows.Interop;

namespace UsbMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += (o, e) =>
            {
                ((UsbDetectViewModel)this.DataContext).RegistHwnd(new WindowInteropHelper(this).Handle);
                ((UsbDetectViewModel)this.DataContext).ToastNotified += OnToastNotified;
                this.Visibility = Visibility.Hidden;
            };
        }

        private void OnClosed(object sender, EventArgs e)
        {
            var fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dirName = ((UsbDetectViewModel)this.DataContext).LogDir;
            ((UsbDetectViewModel)this.DataContext).ExportNotify($"{dirName}{(System.IO.Path.DirectorySeparatorChar)}{fileName}.log");

            ((UsbDetectViewModel)this.DataContext).Dispose();

            ToastNotificationManagerCompat.Uninstall();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void OnDirSelectBtnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = ((UsbDetectViewModel)this.DataContext).LogDir;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ((UsbDetectViewModel)this.DataContext).LogDir = dialog.SelectedPath;
            }
        }

        private void OnToastNotified(DeviceDetector.DeviceNotifyEventArg notifyInfo)
        {
            if (notifyInfo != null)
            {
                new ToastContentBuilder().AddText($"{(notifyInfo.DeviceNameAlias == string.Empty ? notifyInfo.DeviceName : notifyInfo.DeviceNameAlias)} が{(notifyInfo.IsAdded ? "接続され" : "抜かれ")}ました。")
                    .AddText($"製造者：{(notifyInfo.ManufacturerAlias == string.Empty ? notifyInfo.Manufacturer : notifyInfo.ManufacturerAlias)}")
                    .AddAppLogoOverride(new Uri(Path.GetFullPath("UsbMonitor48.png"), UriKind.Relative))
                    .Show();
            }
        }

        private void OnMenuClicked(object sender, RoutedEventArgs e)
        {
            if (this.notifyList.SelectedIndex >= 0 && sender.GetType() == typeof(System.Windows.Controls.MenuItem))
            {
                foreach (var notify in ((UsbDetectViewModel)this.DataContext).NotifyList)
                {
                    if (notify != null && notify.IsSelected)
                    {
                        if (((System.Windows.Controls.MenuItem)sender).Name == "Detail")
                        {
                            // TODO: not implement
                        }
                        if (((System.Windows.Controls.MenuItem)sender).Name == "Alias")
                        {
                            // TODO: not implement
                        }
                        break;
                    }
                }
            }
        }

        private void OnNotifyListMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            foreach (var notify in ((UsbDetectViewModel)this.DataContext).NotifyList)
            {
                if (notify != null && notify.IsSelected) 
                {
                    var detail = new Detail(notify);
                    var result = detail.ShowDialog();
                    if (result is not null && result is true)
                    {
                        ((UsbDetectViewModel)this.DataContext).UpdateNotify(notify);
                        break;
                    }
                }
            }
        }

        private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape) this.Hide();
        }
    }
}