using Microsoft.Toolkit.Uwp.Notifications;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using NLog;

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
                // いったん表示しないとウィンドウメッセージを受け取れないため起動後即非表示
                this.Visibility = Visibility.Hidden;
            };
        }

        /// <summary>
        /// Closedイベントハンドラ。
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnClosed(object sender, EventArgs e)
        {
            // 通知ログを出力
            var fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dirName = ((UsbDetectViewModel)this.DataContext).LogDir;
            ((UsbDetectViewModel)this.DataContext).ExportNotify($"{dirName}{(System.IO.Path.DirectorySeparatorChar)}{fileName}.log");

            ((UsbDetectViewModel)this.DataContext).Dispose();

            // トースト通知後始末
            ToastNotificationManagerCompat.Uninstall();
        }

        /// <summary>
        /// <br>Closingイベントハンドラ。</br>
        /// <br>終了はトレイアイコンメニューから行うため、終了はキャンセルして画面非表示のみ。</br>
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        /// <summary>
        /// 出力ディレクトリ選択ボタンイベントハンドラ。
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnDirSelectBtnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = ((UsbDetectViewModel)this.DataContext).LogDir;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ((UsbDetectViewModel)this.DataContext).LogDir = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// トースト通知イベントハンドラ。
        /// </summary>
        /// <param name="notifyInfo"></param>
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

        /// <summary>
        /// 通知リストのダブルクリックイベントハンドラ。
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnNotifyListMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.Source.GetType() == typeof(System.Windows.Controls.DataGridRow) &&
                ((System.Windows.Controls.DataGridRow)e.Source).Item is not null &&
                ((System.Windows.Controls.DataGridRow)e.Source).Item.GetType() == typeof(DeviceNotifyInfomation))
            {
                DeviceNotifyInfomation notify = (DeviceNotifyInfomation)((System.Windows.Controls.DataGridRow)e.Source).Item;
                var detail = new Detail(notify);
                var result = detail.ShowDialog();
                if (result is not null && result is true)
                {
                    // OKボタンで終了していたら情報を反映
                    ((UsbDetectViewModel)this.DataContext).UpdateNotify(notify);
                }
            }
        }

        /// <summary>
        /// <br>キー入力イベントハンドラ。</br>
        /// <br>ESCキーで画面表示を消す。</br>
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape) this.Hide();
        }

        private NLog.Logger Logger { get; } = NLog.LogManager.GetCurrentClassLogger();
    }
}