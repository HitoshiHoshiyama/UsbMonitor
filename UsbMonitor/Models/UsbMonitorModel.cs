using NLog;
using DeviceDetector;
using System.Windows.Interop;
using System.IO;

namespace UsbMonitor
{
    /// <summary>Model class</summary>
    internal class UsbMonitorModel : IDisposable
    {
        /// <summary>コンストラクタ。</summary>
        public UsbMonitorModel()
        {
            this.Logger = LogManager.GetCurrentClassLogger();
            this.Detector = new DeviceDetector.DeviceDetector(this.Logger);
        }

        /// <summary>
        /// 通知リストをファイル出力する。
        /// </summary>
        /// <param name="notifyList">デバイス変更通知リストを指定する。</param>
        /// <param name="fileName">ファイル名をフルパスで指定する。</param>
        public void Export(List<DeviceNotifyEventArg> notifyList, string fileName)
        {
            // 出力するものが無い時はファイルを作成しない
            if (notifyList is null || notifyList.Count == 0) return;

            using (var outFile = new StreamWriter(fileName, false))
            {
                foreach (var item in notifyList)
                {
                    if(item is not null)
                    {
                        var line = $"{(item.DateTime.ToString("yyyy/MM/dd HH:mm:ss"))}\t{(item.IsAdded ? "add" : "remove")}\t";
                        line += $"{item.DeviceName}\t{item.Manufacturer}\t{item.PnPDeviceId}{Environment.NewLine}";
                        outFile.Write(line);
                    }
                }
            }
        }

        /// <summary>
        /// ウィンドウハンドルを登録する。
        /// </summary>
        /// <param name="Hwnd">ウィンドウハンドルを指定する。</param>
        public void RegistWindowHandle(IntPtr Hwnd)
        {
            this.Detector.SetWindowHandle(Hwnd);
            this.Detector.DeviceChanged += this.OnDetect;
            var hwndSrc = HwndSource.FromHwnd(Hwnd);
            hwndSrc.AddHook(new HwndSourceHook(this.Detector.WndProc));
        }

        /// <summary>
        /// <br>デバイスの別名を登録する。</br>
        /// <br>登録された内容はJSONに保存され、次回以降の検出時に使用される。</br>
        /// </summary>
        /// <param name="deviceNameAlias">デバイス名の別名を指定する。</param>
        /// <param name="manufacturerAlias">製造者名の別名を指定する。</param>
        /// <param name="notify">名前を置き換えるデバイスの、デバイス通知情報を指定する。</param>
        public void RegistDeviceAlias(string deviceNameAlias, string manufacturerAlias, DeviceNotifyInfomation notify)
        {
            this.Detector.RegistDeviceAlias(deviceNameAlias, manufacturerAlias, notify);
        }

        public event EventHandler<DeviceNotifyEventArg>? DeviceChanged;

        /// <summary>
        /// デバイス変更検知イベントハンドラ。
        /// </summary>
        /// <param name="sender">イベント発生元オブジェクトが設定される。</param>
        /// <param name="e">イベント引数が設定される。</param>
        private void OnDetect(object sender, DeviceDetector.DeviceNotifyEventArg e)
        {
            this.Logger.Debug($"{e.DeviceName} is {(e.IsAdded ? "added" : "removed")}.");
            this.DeviceChanged?.Invoke( this, e );
        }

        /// <summary>リソースを解放する。</summary>
        public void Dispose() { this.Detector.Dispose(); }
        /// <summary>デバイス変更検知クラスインスタンス。</summary>
        private DeviceDetector.DeviceDetector Detector;
        /// <summary>NLogのロガーインスタンス。</summary>
        private NLog.Logger Logger;
    }
}
