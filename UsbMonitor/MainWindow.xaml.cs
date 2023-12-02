using Microsoft.Toolkit.Uwp.Notifications;
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

        private void OnToastNotified(DeviceDetector.DeviceNotifyInfomation notifyInfo)
        {
            new ToastContentBuilder().AddArgument("action", "viewConversation")
                .AddArgument("conversationId", 9813)
                .AddText($"{notifyInfo.DeviceName} が{(notifyInfo.IsAdded ? "接続され" : "抜かれ")}ました。")
                .AddText($"製造者：{notifyInfo.Manufacturer}")
                .Show();
        }
    }
}