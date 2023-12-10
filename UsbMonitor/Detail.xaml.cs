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
        public Detail(DeviceNotifyInfomation deviceInfo)
        {
            InitializeComponent();

            this.DataContext = new DeviceDetailViewModel(deviceInfo);
        }

        private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape) this.Close();
        }

        private void OnOkClicked(object sender, RoutedEventArgs e)
        {
            ((DeviceDetailViewModel)this.DataContext).UpdateAlias();
            this.DialogResult = true;
            this.Close();
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            if (((DeviceDetailViewModel)this.DataContext).IsAliasUpdate())
            {
                var msg = $"別名が変更されているため、終了すると変更が失われます。{Environment.NewLine}変更を破棄して終了しますか？";
                var result = System.Windows.MessageBox.Show(msg, "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (result == MessageBoxResult.Yes)
                {
                    this.DialogResult = false;
                    this.Close();
                }
            }
            else
            {
                this.DialogResult = false;
                this.Close();
            }
        }

        private void OnEditClick(object sender, RoutedEventArgs e)
        {
            // ボタンに対応したTextBoxと内部プロパティの間でテキストをコピー
            var targetControl = sender == this.BtnDeviceNameEdit ? this.DeviceNameAliasTextBox : this.ManufacturerAliasTextBox;
            targetControl.BorderThickness = new Thickness(0.5);
            targetControl.IsReadOnly = false;
            this.BtnOk.IsEnabled = true;
            ((System.Windows.Controls.Button)sender).IsEnabled = false;
        }

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
