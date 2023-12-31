using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsbMonitor
{
    /// <summary>デバイス詳細のView Model</summary>
    public class DeviceDetailViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="deviceInfo">表示対象のデバイス通知情報を指定する。</param>
        public DeviceDetailViewModel(DeviceNotifyInfomation deviceInfo)
        {
            this.DeviceInfo = deviceInfo;
            this.Root = new List<DeviceNotifyInfomation> { deviceInfo };
            this.ManufacturerAlias = deviceInfo.ManufacturerAlias;
            this.DeviceNameAlias = deviceInfo.DeviceNameAlias;
        }

        /// <summary>デバイス/製造者の別名を更新する。</summary>
        public void UpdateAlias()
        {
            var deviceNameAlias = this.DeviceInfo.DeviceNameAlias;
            var manufacturerNameAlias = this.DeviceInfo.ManufacturerAlias;

            // 変更されている要素だけ置き換える(変化のない要素は元の値で単純に上書き)
            if (deviceNameAlias != this.DeviceNameAlias)
            {
                deviceNameAlias = this.DeviceNameAlias;
            }
            if (manufacturerNameAlias != this.ManufacturerAlias)
            {
                manufacturerNameAlias = this.ManufacturerAlias;
            }
            this.DeviceInfo.SetAlias(deviceNameAlias, manufacturerNameAlias);
        }

        /// <summary>プロパティ変化の通知イベント。</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 別名に変化があったかを返す。
        /// </summary>
        /// <returns>デバイス/製造者いずれかの別名に変更がある場合はtrueが返る。</returns>
        public bool IsAliasUpdate()
        {
            return this.DeviceInfo.DeviceNameAlias != this.DeviceNameAlias ||
                   this.DeviceInfo.ManufacturerAlias != this.ManufacturerAlias;
        }

        /// <summary>対象デバイスのデバイス通知情報を取得する。</summary>
        public DeviceNotifyInfomation DeviceInfo { get; private set; }
        /// <summary>対象デバイスをルートにツリーを取得するためのデバイス通知情報リストを取得する。</summary>
        public List<DeviceNotifyInfomation> Root {  get; private set; }

        /// <summary>製造者の別名を取得・設定する。</summary>
        public string ManufacturerAlias { get; set; }
        /// <summary>デバイス名の別名を取得・設定する。</summary>
        public string DeviceNameAlias { get; set; }
    }
}
