using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Configuration;
using DeviceDetector;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace UsbMonitor
{
    /// <summary>View Model</summary>
    internal class UsbDetectViewModel : INotifyPropertyChanged, IDisposable
    {
        /// <summary>コンストラクタ。</summary>
        public UsbDetectViewModel()
        {
            this.UsbMonitorModel = new UsbMonitorModel();
            // ObservableCollectionをUIスレッド以外(Callbackスレッド)から操作するため
            BindingOperations.EnableCollectionSynchronization(this.notifyList, new object());
        }

        /// <summary>
        /// ウィンドウハンドルをMoedl側に登録する。
        /// </summary>
        /// <param name="Hwnd">ウィンドウハンドルを指定する。</param>
        public void RegistHwnd(IntPtr Hwnd)
        {
            this.UsbMonitorModel.RegistWindowHandle(Hwnd);
            this.UsbMonitorModel.DeviceChanged += OnDeviceChanged;
        }

        /// <summary>
        /// デバイス変更イベントハンドラ。
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="notify">デバイス変更通知情報が設定される。</param>
        private void OnDeviceChanged(object? sender, DeviceDetector.DeviceNotifyInfomation notify)
        {
            this.notifyList.Add(notify);
            this.NotifyList = this.notifyList;
        }

        public void ExportNotify(string fileName)
        {
            this.UsbMonitorModel.Export(this.notifyList.ToList(), fileName);
        }

        /// <summary>リソースを開放する。</summary>
        public void Dispose() { this.UsbMonitorModel.Dispose(); }

        /// <summary>プロパティ変更時に発生するイベント。</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>デバイス変更通知リストを取得・設定する。</summary>
        public ObservableCollection<DeviceNotifyInfomation> NotifyList
        {
            get { return this.notifyList; }
            set
            {
                this.notifyList = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.NotifyList)));
            }
        }

        /// <summary>ログ保存ディレクトリを取得・設定する。</summary>
        public string LogDir
        {
            get
            {
                return this.config.AppSettings.Settings.AllKeys.Contains("logDir") ? this.config.AppSettings.Settings["logDir"].Value : System.IO.Directory.GetCurrentDirectory();
            }
            set
            {
                // Config中に logDir キーがあれば値を更新、なければ作成
                if (this.config.AppSettings.Settings.AllKeys.Contains("logDir")) this.config.AppSettings.Settings["logDir"].Value = value;
                else this.config.AppSettings.Settings.Add("logDir", value);
                // 保存->再読み出し
                this.config.Save(ConfigurationSaveMode.Modified, true);
                this.config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            }
        }

        private UsbMonitorModel UsbMonitorModel;
        private ObservableCollection<DeviceNotifyInfomation> notifyList = new ObservableCollection<DeviceNotifyInfomation>();
        private Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
    }

    /// <summary>Bool型を文字列型に変換するコンバータクラス。</summary>
    internal class BoolToStringConverter : IValueConverter
    {
        /// <summary>falseに対応する漏れ実を取得・設定する。</summary>
        public string FalseStr { get; set; } = string.Empty;
        /// <summary>trueに対応する漏れ実を取得・設定する。</summary>
        public string TrueStr { get; set; } = string.Empty;

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return FalseStr;
            else return (bool)value ? TrueStr : FalseStr;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value != null ? value.Equals(TrueStr) : false;
        }
    }
}
