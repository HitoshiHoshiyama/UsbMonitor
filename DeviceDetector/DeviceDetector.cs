using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Management;
using System.Text;
using System.Text.Json;
using System.IO;
using System.IO.IsolatedStorage;
using System.Net.Security;

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

        private const string MapFileName = "AliasMap.json";
        private const int LOAD_WAIT_TIME = 800;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="logger">NLogのロガーインスタンスを指定する。省略可。</param>
        public DeviceDetector(NLog.Logger? logger = null)
        {
            if (logger is not null) this.LoggerInstance = logger;
            this.isoStorage = IsolatedStorageFile.GetUserStoreForApplication();
            var isoFileExist = isoStorage.FileExists(MapFileName);
            if (isoFileExist || System.IO.File.Exists(MapFileName))
            {
                IsolatedStorageFileStream? isoStream = null;
                // 分離ストレージにファイルが存在する場合はそちら優先
                if (isoFileExist) isoStream = new IsolatedStorageFileStream(MapFileName, FileMode.Open, FileAccess.Read, this.isoStorage);
                using (var reader = isoStream is null ? new StreamReader(MapFileName) : new StreamReader(isoStream))
                {
                    if (reader is not null)
                    {
                        var element = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(reader.ReadToEnd());
                        if (element is not null)
                        {
                            this.AliasMap = element;
                            this.LoggerInstance?.Info("Loading alias map is succeeded.");
                        }
                    }
                }
            }
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
        /// <br>デバイスの別名を登録する。</br>
        /// <br>登録された内容はJSONに保存され、次回以降の検出時に使用される。</br>
        /// </summary>
        /// <param name="deviceNameAlias">デバイス名の別名を指定する。</param>
        /// <param name="manufacturerAlias">製造者名の別名を指定する。</param>
        /// <param name="deviceInfo">名前を置き換えるデバイスの、デバイス追加/削除イベント引数を指定する。</param>
        public void RegistDeviceAlias(string deviceNameAlias, string manufacturerAlias, DeviceNotifyEventArg deviceInfo)
        {
            var properties = new Dictionary<string, string>()
            {
                {"deviceName", deviceInfo.DeviceName},
                {"manufacturer", deviceInfo.Manufacturer},
                {"pnpDeviceId", deviceInfo.PnPDeviceId},
                {"classGuid", deviceInfo.ClassGuid.ToString()},
                {"className", deviceInfo.ClassName },
                {"vid", deviceInfo.Vid},
                {"pid", deviceInfo.Pid},
                {"deviceNameAlias", deviceNameAlias},
                {"manufacturerAlias", manufacturerAlias},
            };
            if (this.AliasMap.ContainsKey(deviceInfo.PnPDeviceId))
            {
                this.AliasMap[deviceInfo.PnPDeviceId] = properties;
            }
            else
            {
                this.AliasMap.Add(deviceInfo.PnPDeviceId, properties);
            }
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

            var isoStream = new IsolatedStorageFileStream(MapFileName, FileMode.Create, FileAccess.Write, this.isoStorage);
            using (var writer = new StreamWriter(isoStream, Encoding.UTF8))
            {
                if (writer is not null)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var element = JsonSerializer.Serialize<Dictionary<string, Dictionary<string, string>>>(this.AliasMap, options);
                    if (element is not null)
                    {
                        writer.Write(element);
                        this.LoggerInstance?.Info("Saving alias map is succeeded.");
                    }
                }
            }
            this.isoStorage.Dispose();

            if (this.HdevNotify != IntPtr.Zero) { UnregisterDeviceNotification(this.HdevNotify); }
            this.LoggerInstance?.Info($"{this.GetType().Name} all resource disposed.");
        }

        /// <summary>デバイスの追加/削除時に発生するイベント。</summary>
        public event DeviceChanded? DeviceChanged;

        /// <summary>
        /// デバイス追加/削除イベントのデリゲート。
        /// </summary>
        /// <param name="sender">イベント発生元のインスタンスを指定する。</param>
        /// <param name="eventArg">イベント情報を設定したイベント引数を指定する。</param>
        public delegate void DeviceChanded(object sender, DeviceNotifyEventArg eventArg);

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
                        Thread.Sleep(LOAD_WAIT_TIME);
                        pnpEntity = new ManagementClass("Win32_PnPEntity").GetInstances();
                    }
                    // デバイスパスからVID/PIDを抽出
                    var dbcc_name = request.Item2.dbcc_name is null ? string.Empty : new string(request.Item2.dbcc_name);
                    dbcc_name = TrimDevicePath(dbcc_name.Remove(dbcc_name.IndexOf('\0')));
                    DeviceNotifyEventArg arg;
                    if (instance.AliasMap.ContainsKey(dbcc_name))
                    {
                        arg = new DeviceNotifyEventArg(request.Item1, instance.AliasMap[dbcc_name]);
                    }
                    else
                    {
                        arg = new DeviceNotifyEventArg(request.Item1, dbcc_name, pnpEntity, instance.TaskCancel, instance.LoggerInstance);
                    }

                    if (arg.DeviceName != string.Empty)
                    {
                        // VID/PIDが一致した場合はイベント発火
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

        /// <summary>
        /// dbcc_nameから不要な文字を削除する。
        /// </summary>
        /// <param name="dbcc_name">構造体 DEV_BROADCAST_DEVICEINTERFACE の dbcc_name を指定する。</param>
        /// <returns>dbcc_name から不要な情報を省いたデバイスIDを返す。</returns>
        private static string TrimDevicePath(string? dbcc_name)
        {
            if (dbcc_name is null) return string.Empty;

            var result = dbcc_name.ToUpper();
            var UsbPos = result.IndexOf("USB");
            if (UsbPos < 0) return string.Empty;
            result = result.Substring(UsbPos);
            var classId = result.IndexOf("{");
            if (classId >= 0) result = result.Substring(0, classId);
            if (result.ElementAt(result.Length - 1) == '#') result = result.Substring(0, result.Length - 1);
            result = result.Replace("#", @"\");

            return result;
        }

        private Task MsgTask;
        private BlockingCollection<Tuple<bool, DEV_BROADCAST_DEVICEINTERFACE>> TaskQueue;
        private CancellationTokenSource TaskCancel = new CancellationTokenSource();
        private NLog.Logger? LoggerInstance;
        private IntPtr HdevNotify = IntPtr.Zero;

        private IsolatedStorageFile isoStorage;

        private Dictionary<string, Dictionary<string, string>> AliasMap = new Dictionary<string, Dictionary<string, string>>();
    }

    /// <summary>デバイス追加/削除イベントのイベント引数クラス。</summary>
    public class DeviceNotifyEventArg : EventArgs
    {
        #region native
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int CM_Locate_DevNode(ref int pdnDevInst, string pDeviceID, int ulFlags);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int CM_Get_Child(ref int pdnDevInst, int dnDevInst, int ulFlags = 0);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int CM_Get_Sibling(ref int pdnDevInst, int dnDevInst, int ulFlags = 0);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int CM_Get_Device_ID_Size(out uint pulLen, int dnDevInst, int flags = 0);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int CM_Get_Device_ID(int dnDevInst, StringBuilder Buffer, uint BufferLen, int ulFlags = 0);

        private const int CM_LOCATE_DEVNODE_NORMAL = 0;
        private const int CM_LOCATE_DEVNODE_PHANTOM = 1;

        private const int CR_SUCCESS = 0;
        #endregion

        /// <summary>
        /// コピーコンストラクタ。
        /// </summary>
        /// <param name="eventArg">コピー元オブジェクトを指定する。</param>
        public DeviceNotifyEventArg(DeviceNotifyEventArg eventArg)
        {
            this.IsAdded = eventArg.IsAdded;
            this.DateTime = eventArg.DateTime;
            this.DeviceName = eventArg.DeviceName;
            this.Manufacturer = eventArg.Manufacturer;
            this.PnPDeviceId = eventArg.PnPDeviceId;
            this.ClassGuid = eventArg.ClassGuid;
            this.ClassName = eventArg.ClassName;
            this.Vid = eventArg.Vid;
            this.Pid = eventArg.Pid;
            this.DeviceNameAlias = eventArg.DeviceNameAlias;
            this.ManufacturerAlias = eventArg.ManufacturerAlias;
            this.Childs = eventArg.Childs;
        }

        /// <summary>
        /// <br>プロパティリストから情報を設定して DeviceNotifyEventArg クラスを初期化する。</br>
        /// <br>この方法の場合、Childプロパティは空のリストを返す。</br>
        /// </summary>
        /// <param name="isAdded">追加/削除の区分を指定する。trueが追加を意味する。</param>
        /// <param name="properties">プロパティリストを指定する。</param>
        public DeviceNotifyEventArg(bool isAdded, Dictionary<string, string> properties)
        {
            this.IsAdded = isAdded;

            this.DeviceName = properties["deviceName"];
            this.Manufacturer = properties["manufacturer"];
            this.PnPDeviceId = properties["pnpDeviceId"];
            this.ClassGuid = properties.TryGetValue("classGuid", out string? value) && value != string.Empty ? new Guid(value) : Guid.Empty;
            this.ClassName = properties["className"];
            this.Vid = properties["vid"];
            this.Pid = properties["pid"];
            this.DeviceNameAlias = properties["deviceNameAlias"];
            this.ManufacturerAlias = properties["manufacturerAlias"];
        }

        /// <summary>
        /// デバイスパスから情報を検索して DeviceNotifyEventArg クラスを初期化する。
        /// </summary>
        /// <param name="isAdded">追加/削除の区分を指定する。trueが追加を意味する。</param>
        /// <param name="devicePath">デバイスパスを指定する。</param>
        /// <param name="pnpEntity">Win32_PnPEntityクラスのManagementBaseObjectインスタンスを指定する。</param>
        /// <param name="cancel">デバイス検索を強制終了させる CancellationTokenSource を指定する。</param>
        public DeviceNotifyEventArg(bool isAdded, string devicePath, ManagementObjectCollection pnpEntity, CancellationTokenSource cancel, NLog.Logger? logger = null)
        {
            this.logger = logger;
            this.IsAdded = isAdded;
            var properties = this.GetPropertySet(devicePath, pnpEntity);
            this.DeviceName = properties["deviceName"];
            this.Manufacturer = properties["manufacturer"];
            this.PnPDeviceId = properties["pnpDeviceId"];
            this.ClassGuid = properties.TryGetValue("classGuid", out string? value) && value != string.Empty ? new Guid(value) : Guid.Empty;
            this.ClassName = properties["className"];
            this.Vid = properties["vid"];
            this.Pid = properties["pid"];

            if (DeviceName != string.Empty)
            {
                var pdnDevInst = 0;
                if (CM_Locate_DevNode(ref pdnDevInst, this.PnPDeviceId, CM_LOCATE_DEVNODE_PHANTOM) == CR_SUCCESS) this.Childs = GetChildren(pdnDevInst, pnpEntity, cancel);
            }
            this.logger?.Debug($"Child node:{this.Childs.Count}");
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
        public string PnPDeviceId { get; private set; } = string.Empty;
        /// <summary>Class GUIDを取得する。</summary>
        public Guid ClassGuid { get; private set; } = Guid.Empty;
        /// <summary>デバイスクラス名を取得する。</summary>
        public string ClassName { get; private set; } = string.Empty;
        /// <summary>VenderIDを取得する。</summary>
        public string Vid { get; private set; } = string.Empty;
        /// <summary>ProductIDを取得する。</summary>
        public string Pid { get; private set; } = string.Empty;
        /// <summary>デバイス名のエイリアスを取得する。</summary>
        public string DeviceNameAlias { get; protected set; } = string.Empty;
        /// <summary>製造者名のエイリアスを取得する。</summary>
        public string ManufacturerAlias { get; protected set; } = string.Empty;
        /// <summary>子デバイスを取得する。</summary>
        public List<DeviceNotifyEventArg> Childs { get; protected set; } = new List<DeviceNotifyEventArg>();

        /// <summary>
        /// ManagementClassインスタンスからデバイスパスと一致するデバイス情報を取得する。
        /// </summary>
        /// <param name="devicePath">検索対象のデバイスパスを指定する。</param>
        /// <param name="pnpEntity">Win32_PnPEntityクラスのManagementBaseObjectインスタンスを指定する。</param>
        /// <returns>取得したデバイス情報の連想配列を返す。</returns>
        private Dictionary<string, string> GetPropertySet(string devicePath, ManagementObjectCollection? pnpEntity)
        {
            if (pnpEntity is null) return new Dictionary<string, string>();

            var result = new Dictionary<string, string>();
            var vidPos = devicePath.IndexOf("VID_");
            var pidPos = devicePath.IndexOf("PID_");
            var vid = vidPos >= 0 ? devicePath.Substring(vidPos, 8) : string.Empty;
            var pid = pidPos >= 0 ? devicePath.Substring(pidPos, 8) : string.Empty;
            var deviceName = string.Empty;
            var manufacturer = string.Empty;
            var pnpDeviceId = string.Empty;
            var deviceId = string.Empty;
            var classGuid = string.Empty;
            var className = string.Empty;

            foreach (var device in pnpEntity)
            {
                deviceId = device.GetPropertyValue("DeviceID") is null ? string.Empty : device.GetPropertyValue("DeviceID").ToString();
                if (deviceId is not null && deviceId.ToUpper() == devicePath)
                {
                    // VID/PIDが一致したdeviceの情報からイベント引数にセットする情報を抽出
                    deviceName = device.GetPropertyValue("Name") is null ? string.Empty : device.GetPropertyValue("Name").ToString();
                    manufacturer = device.GetPropertyValue("Manufacturer") is null ? string.Empty : device.GetPropertyValue("Manufacturer").ToString();
                    pnpDeviceId = device.GetPropertyValue("PNPDeviceID") is null ? string.Empty : device.GetPropertyValue("PNPDeviceID").ToString();
                    classGuid = device.GetPropertyValue("ClassGuid") is null ? string.Empty : device.GetPropertyValue("ClassGuid").ToString();
                    className = device.GetPropertyValue("PNPClass") is null ? string.Empty : device.GetPropertyValue("PNPClass").ToString();
                    this.logger?.Debug($"Device name:{(deviceName)} Manufacturer:{manufacturer} ID:{(deviceId)}");
                    break;
                }
                deviceId = string.Empty;
            }
            result["deviceName"] = deviceName is null ? string.Empty : deviceName;
            result["manufacturer"] = manufacturer is null ? string.Empty : manufacturer;
            result["pnpDeviceId"] = pnpDeviceId is null ? string.Empty : pnpDeviceId;
            result["classGuid"] = classGuid is null ? string.Empty : classGuid;
            result["className"] = className is null ? string.Empty : className;
            result["deviceId"] = deviceId is null ? string.Empty : deviceId;
            result["vid"] = vid is null ? string.Empty : vid;
            result["pid"] = pid is null ? string.Empty : pid;

            return result;
        }

        /// <summary>
        /// 指定したデバイスインスタンスを起点に、子デバイスを取得する。
        /// </summary>
        /// <param name="rootDevInst">検索の起点となるデバイスインスタンスを指定する。</param>
        /// <param name="pnpEntity">ManagementClassインスタンスを指定する。</param>
        /// <param name="cancel">デバイス検索を強制終了させる CancellationTokenSource を指定する。</param>
        /// <returns>子デバイスのリストを返す。</returns>
        private List<DeviceNotifyEventArg> GetChildren(int rootDevInst, ManagementObjectCollection pnpEntity, CancellationTokenSource cancel)
        {
            var result = new List<DeviceNotifyEventArg>();
            var next = 0;
            if (cancel.IsCancellationRequested) return result;

            if (CM_Get_Child(ref next, rootDevInst) == CR_SUCCESS)
            {
                var nextId = GetDeviceId(next);
                result.Add(new DeviceNotifyEventArg(this.IsAdded, nextId, pnpEntity, cancel , this.logger));
                rootDevInst = next;
                while (!cancel.IsCancellationRequested && CM_Get_Sibling(ref next, rootDevInst) == CR_SUCCESS)
                {
                    nextId = GetDeviceId(next);
                    result.Add(new DeviceNotifyEventArg(this.IsAdded, nextId, pnpEntity, cancel, this.logger));
                    rootDevInst = next;
                }
            }

            return result;
        }

        /// <summary>
        /// デバイスインスタンスからデバイスIDを取得する。
        /// </summary>
        /// <param name="devInst">デバイスインスタンスを指定する。</param>
        /// <returns>デバイスIDを返す。</returns>
        private static string GetDeviceId(int devInst)
        {
            uint idSize = 0;
            var result = string.Empty;

            CM_Get_Device_ID_Size(out idSize, devInst);
            idSize++;               // Fix: 文字化けしたから入れた(結果的に直ったように見える)けど、本当にこれでいいのかは未確認
            var idBuff = new StringBuilder((int)idSize);
            if (idBuff is not null)
            {
                CM_Get_Device_ID(devInst, idBuff, idSize);
                result = idBuff.ToString();
            }

            return result is null ? string.Empty : result;
        }

        private NLog.Logger? logger;
    }
}
