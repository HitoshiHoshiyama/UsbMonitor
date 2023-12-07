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
                new ToastContentBuilder().AddText($"{notifyInfo.DeviceName} が{(notifyInfo.IsAdded ? "接続され" : "抜かれ")}ました。")
                    .AddText($"製造者：{notifyInfo.Manufacturer}")
                    .AddAppLogoOverride(new Uri(Path.GetFullPath("UsbMonitor48.png"), UriKind.Relative))
                    .Show();
            }
        }

        private void contextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (contextMenu.IsOpen && this.notifyList.SelectedIndex >= 0)
            {
            }
        }
    }
}