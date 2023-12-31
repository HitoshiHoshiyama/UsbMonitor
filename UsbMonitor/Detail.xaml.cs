using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Data;

namespace UsbMonitor
{
    /// <summary>
    /// Detail.xaml の相互作用ロジック
    /// </summary>
    public partial class Detail : Window
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="deviceInfo">デバイス通知情報を指定する。</param>
        public Detail(DeviceNotifyInfomation deviceInfo)
        {
            InitializeComponent();

            // DeviceDetailViewModelのコンストラクタに引数が必要なのでコードビハインドでDataContext設定
            this.DataContext = new DeviceDetailViewModel(deviceInfo);
        }

        /// <summary>
        /// <br>キー入力イベントハンドラ。</br>
        /// <br>ESCキーで画面表示を消す。</br>
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape) this.OnCancelClicked(sender, e);
        }

        /// <summary>
        /// OKボタンクリックイベントハンドラ。
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnOkClicked(object sender, RoutedEventArgs e)
        {
            ((DeviceDetailViewModel)this.DataContext).UpdateAlias();
            // 親画面に別名更新を通知する
            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// キャンセルボタンクリックイベントハンドラ。
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            // TODO: メッセージボックスが画面中央に表示されるため、要対策(別に画面を起こす)
            if (((DeviceDetailViewModel)this.DataContext).IsAliasUpdate())
            {
                var msg = $"別名が変更されているため、終了すると変更が失われます。{Environment.NewLine}変更を破棄して終了しますか？";
                var result = System.Windows.MessageBox.Show(msg, "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (result == MessageBoxResult.Yes)
                {
                    // 親画面に別名更新なしを通知する
                    this.DialogResult = false;
                    this.Close();
                }
                // Noの場合は終了を中止
            }
            else
            {
                // 親画面に別名更新なしを通知する
                this.DialogResult = false;
                this.Close();
            }
        }

        /// <summary>
        /// 編集ボタンクリックイベントハンドラ。
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnEditClick(object sender, RoutedEventArgs e)
        {
            // ボタンに対応したTextBoxと内部プロパティの間でテキストをコピー
            var targetControl = sender == this.BtnDeviceNameEdit ? this.DeviceNameAliasTextBox : this.ManufacturerAliasTextBox;
            targetControl.BorderThickness = new Thickness(0.5);
            targetControl.IsReadOnly = false;
            this.BtnOk.IsEnabled = true;
            ((System.Windows.Controls.Button)sender).IsEnabled = false;
        }

        /// <summary>
        /// <br>コンテキストメニュークリックイベントハンドラ。</br>
        /// <br>選択したツリービューアイテムの名前をクリップボードにコピーする。</br>
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnMenuClicked(object sender, RoutedEventArgs e)
        {
            if (this.DeviceTree.SelectedItem is not null)
            {
                string copyString;
                if (((System.Windows.Controls.MenuItem)sender).Name == "CopyDevice")
                {
                    copyString = ((DeviceNotifyInfomation)this.DeviceTree.SelectedItem).DeviceName;
                }
                else
                {
                    copyString = ((DeviceNotifyInfomation)this.DeviceTree.SelectedItem).Manufacturer;
                }
                System.Windows.Clipboard.SetText(copyString);
            }
        }
    }
}
