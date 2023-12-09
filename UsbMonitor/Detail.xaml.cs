using System.Windows;
using System.Windows.Input;

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
    }
}
