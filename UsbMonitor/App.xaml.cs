using System.Windows;

namespace UsbMonitor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        /// <summary>
        /// スタートアップハンドラのオーバーライド
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // トレイアイコンとメニューを設定
            var icon = GetResourceStream(new Uri("UsbMonitor48.ico", UriKind.Relative)).Stream;
            var menu = new ContextMenuStrip();
            menu.Items.Add("終了", null, ExitMenuClick);
            this.NotifyIcon = new NotifyIcon
            {
                Visible = true,
                Icon = new System.Drawing.Icon(icon),
                Text = $"{this.GetType().Namespace}",
                ContextMenuStrip = menu
            };
            // マウスイベントハンドラを設定
            this.NotifyIcon.MouseDoubleClick += new MouseEventHandler(OnDoubleClick);
        }

        /// <summary>
        /// トレイアイコンダブルクリックハンドラ
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクト(Icon)が設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnDoubleClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            // 初回はMainWindow未生成(XAMLのStartupUriは削除)
            if (this.mainWindow is null) this.mainWindow = new MainWindow();
            // MainWindowの表示(×ボタンではHideするだけ)
            this.mainWindow.Show();
        }

        /// <summary>
        /// コンテキストメニューの終了選択時ハンドラ
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクト(Icon)が設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void ExitMenuClick(object? sender, EventArgs e)
        {
            this.NotifyIcon?.Dispose();
            Shutdown();
        }

        private MainWindow? mainWindow = null;
        private NotifyIcon? NotifyIcon = null;
    }
}
