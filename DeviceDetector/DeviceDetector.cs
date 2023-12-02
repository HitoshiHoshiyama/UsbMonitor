using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Management;

namespace DeviceDetector
{
    /// <summary>デバイス追加/削除を検知するクラス。</summary>
    public class DeviceDetector : IDisposable
    {
        #region WIN32_DEFINE
        private const int WM_DEVICECHANGE = 0x219;

        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        private const int DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;

        private readonly Guid GUID_DEVINTERFACE_USB_DEVICE = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");

        private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

        private const int DBCC_NAME_LENGTH = 256;

        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_HDR
        {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = DBCC_NAME_LENGTH)]
            public char[] dbcc_name;
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr recipient, IntPtr notificationFilter, int flags);
        [DllImport("user32.dll")]
        private static extern bool UnregisterDeviceNotification(IntPtr handle);
        #endregion

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="logger">NLogのロガーインスタンスを指定する。省略可。</param>
        public DeviceDetector(NLog.Logger? logger = null)
        {
            if (logger is not null) this.LoggerInstance = logger;
            // Task起動準備
            this.TaskQueue = new BlockingCollection<Tuple<bool, DEV_BROADCAST_DEVICEINTERFACE>>();
            this.MsgTask = new Task(() => this.MsgTaskProc(this));
            // Task起動
            this.MsgTask.Start();
            this.LoggerInstance?.Info($"{this.GetType().Name} initialize complete.");
        }

        /// <summary>
        /// DBT_DEVICEARRIVAL受信登録(RegisterDeviceNotification)を行う。
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドルを指定する。</param>
        /// <returns></returns>
        public bool SetWindowHandle(IntPtr hWnd)
        {
            var deviceIf = new DEV_BROADCAST_DEVICEINTERFACE()
            {
                dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
                dbcc_classguid = GUID_DEVINTERFACE_USB_DEVICE,
                dbcc_reserved = 0,
                dbcc_name = new char[DBCC_NAME_LENGTH]
            };
            deviceIf.dbcc_size = Marshal.SizeOf(deviceIf);
            IntPtr NotificationFilter = Marshal.AllocHGlobal(deviceIf.dbcc_size);
            Marshal.StructureToPtr<DEV_BROADCAST_DEVICEINTERFACE>(deviceIf, NotificationFilter, true);
            this.HdevNotify = RegisterDeviceNotification(hWnd, NotificationFilter, DEVICE_NOTIFY_WINDOW_HANDLE);
            Marshal.FreeHGlobal(NotificationFilter);

            this.LoggerInstance?.Debug($"RegisterDeviceNotification result: 0x{this.HdevNotify:x8}");
            if (this.HdevNotify == IntPtr.Zero) this.LoggerInstance?.Debug($"GetLastError: 0x{Marshal.GetLastWin32Error():x8}");

            return this.HdevNotify != IntPtr.Zero;
        }

        /// <summary>
        /// ウィンドウプロシージャ。WM_DEVICECHANGEのみ処理する。
        /// </summary>
        /// <param name="hwnd">ウィンドウハンドルが設定される。</param>
        /// <param name="msg">ウィンドウメッセージが設定される。</param>
        /// <param name="wParam">デバイスイベントが設定される。</param>
        /// <param name="lParam">デバイス識別構造体のポインタが設定される。</param>
        /// <param name="handled">イベント結果を処理済みとしてマークするかどうかを指定する。</param>
        /// <returns>子ウィンドウのウィンドウハンドルを返す。</returns>
        public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE)
            {
                if(wParam.ToInt32() == DBT_DEVICEARRIVAL || wParam.ToInt32() == DBT_DEVICEREMOVECOMPLETE)
                {
                    var hdr = Marshal.PtrToStructure<DEV_BROADCAST_HDR>(lParam);
                    if(hdr.dbch_devicetype == DBT_DEVTYP_DEVICEINTERFACE)
                    {
                        var devif = Marshal.PtrToStructure<DEV_BROADCAST_DEVICEINTERFACE>(lParam);
                        this.TaskQueue.Add(new Tuple<bool, DEV_BROADCAST_DEVICEINTERFACE>(wParam.ToInt32() == DBT_DEVICEARRIVAL, devif));
                    }
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>デバイスの追加/削除時に発生するイベント。</summary>
        public event DeviceChanded? DeviceChanged;

        /// <summary>
        /// デバイス追加/削除イベントのデリゲート。
        /// </summary>
        /// <param name="sender">イベント発生元のインスタンスを指定する。</param>
        /// <param name="eventArg">イベント情報を設定したイベント引数を指定する。</param>
        public delegate void DeviceChanded(object sender, DeviceNotifyInfomation eventArg);

        /// <summary>デバイス追加/削除時の処理を行うTask。</summary>
        private Action<DeviceDetector> MsgTaskProc = ((DeviceDetector instance) =>
        {
            // デバイス削除時に備えて予めManagementClassインスタンスを作成しておく
            var pnpEntity = new ManagementClass("Win32_PnPEntity").GetInstances();
            while (true)
            {
                try
                {
                    instance.LoggerInstance?.Info("Queue waiting...");
                    var request = instance.TaskQueue.Take(instance.TaskCancel.Token);
                    if (request is null) continue;
                    if (request.Item1)
                    {
                        // device追加時はManagementClass再生成
                        pnpEntity.Dispose();
                        pnpEntity = new ManagementClass("Win32_PnPEntity").GetInstances();
                    }
                    // デバイスパスからVID/PIDを抽出
                    var devicePath = request.Item2.dbcc_name is null ? string.Empty : new string(request.Item2.dbcc_name);
                    devicePath = devicePath.Remove(devicePath.IndexOf('\0')).ToUpper();
                    var vid = devicePath.Substring(devicePath.IndexOf("VID_"), 8);
                    var pid = devicePath.Substring(devicePath.IndexOf("PID_"), 8);
                    var deviceName = string.Empty;
                    var manufacturer = string.Empty;
                    var pnpDeviceId = string.Empty;

                    foreach (var device in pnpEntity)
                    {
                        var deviceId = device.GetPropertyValue("DeviceID") is null ? string.Empty : device.GetPropertyValue("DeviceID").ToString();
                        if (deviceId is not null && deviceId.ToUpper().IndexOf(vid) >= 0 && deviceId.ToUpper().IndexOf(pid) >= 0)
                        {
                            // VID/PIDが一致したdeviceの情報からイベント引数にセットする情報を抽出
                            deviceName = device.GetPropertyValue("Name") is null ? string.Empty : device.GetPropertyValue("Name").ToString();
                            manufacturer = device.GetPropertyValue("Manufacturer") is null ? string.Empty : device.GetPropertyValue("Manufacturer").ToString();
                            pnpDeviceId = device.GetPropertyValue("PNPDeviceID") is null ? string.Empty : device.GetPropertyValue("PNPDeviceID").ToString();
                            instance.LoggerInstance?.Debug($"Device name:{(deviceName)} Manufacturer:{manufacturer} ID:{(deviceId)}");
                            break;
                        }
                    }

                    if (deviceName != string.Empty)
                    {
                        // VID/PIDが一致した場合はイベント発火
                        var arg = new DeviceNotifyInfomation( request.Item1,
                                                            deviceName is null ? string.Empty : deviceName,
                                                            manufacturer is null ? string.Empty : manufacturer,
                                                            pnpDeviceId is null ? string.Empty : pnpDeviceId);
                        instance.LoggerInstance?.Debug($"Begin callback(isAdded:{arg.IsAdded} name:{arg.DeviceName})");
                        instance.DeviceChanged?.Invoke(instance, arg);
                    }
                }
                catch (OperationCanceledException)  // 終了要求
                {
                    instance.LoggerInstance?.Debug("Cancel requested.");
                    break;
                }
                catch (Exception ex)                // その他の例外発生時は処理をキャンセルしてキューWaitへ復帰
                {
                    instance.LoggerInstance?.Warn(ex.ToString());
                    continue;
                }
            }
            pnpEntity.Dispose();
        });
        private Task MsgTask;
        private BlockingCollection<Tuple<bool, DEV_BROADCAST_DEVICEINTERFACE>> TaskQueue;
        private CancellationTokenSource TaskCancel = new CancellationTokenSource();
        private NLog.Logger? LoggerInstance;
        private IntPtr HdevNotify = IntPtr.Zero;

        /// <summary>リソース破棄処理。</summary>
        public void Dispose()
        {
            // Task終了->終了待ち
            this.TaskCancel.Cancel();
            this.LoggerInstance?.Debug("MessageTask canceled.");
            this.MsgTask.Wait();
            this.LoggerInstance?.Debug("MessageTask terminated.");

            this.TaskQueue.Dispose();
            this.TaskCancel.Dispose();

            if (this.HdevNotify != IntPtr.Zero) { UnregisterDeviceNotification(this.HdevNotify); }
            this.LoggerInstance?.Info($"{this.GetType().Name} all resource disposed.");
        }
    }

    /// <summary>デバイス追加/削除イベントのイベント引数クラス。</summary>
    public class DeviceNotifyInfomation : EventArgs
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="isAdded">追加/削除の区分を指定する。trueが追加を意味する。</param>
        /// <param name="deviceName">デバイス名を指定する。</param>
        /// <param name="manufacturer">製造者名を指定する。</param>
        /// <param name="PnPDeviceId">インスタンスパスを指定する。</param>
        public DeviceNotifyInfomation(bool isAdded, string deviceName, string manufacturer, string PnPDeviceId)
        {
            this.IsAdded = isAdded;
            this.DeviceName = deviceName;
            this.Manufacturer = manufacturer;
            this.InstancePath = PnPDeviceId;
        }
        /// <summary>日時を取得する。</summary>
        public DateTime DateTime { get; private set; } = DateTime.Now;
        /// <summary>追加／削除の区分を取得する。</summary>
        public bool IsAdded { get; private set; }
        /// <summary>デバイス名を取得する。</summary>
        public string DeviceName { get; private set; } = string.Empty;
        /// <summary>製造者名を取得する。</summary>
        public string Manufacturer { get; private set; } = string.Empty;
        /// <summary>インスタンスパスを取得する。</summary>
        public string InstancePath { get; private set; } = string.Empty;
    }
}
