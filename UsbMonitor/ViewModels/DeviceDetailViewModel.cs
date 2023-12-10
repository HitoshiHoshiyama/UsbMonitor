using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsbMonitor
{
    public class DeviceDetailViewModel : INotifyPropertyChanged
    {
        public DeviceDetailViewModel(DeviceNotifyInfomation deviceInfo)
        {
            this.DeviceInfo = deviceInfo;
            this.Root = new List<DeviceNotifyInfomation> { deviceInfo };
            this.ManufacturerAlias = deviceInfo.ManufacturerAlias;
            this.DeviceNameAlias = deviceInfo.DeviceNameAlias;
        }

        public void UpdateAlias()
        {
            var deviceNameAlias = this.DeviceInfo.DeviceNameAlias;
            var manufacturerNameAlias = this.DeviceInfo.ManufacturerAlias;

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

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsAliasUpdate()
        {
            return this.DeviceInfo.DeviceNameAlias != this.DeviceNameAlias ||
                   this.DeviceInfo.ManufacturerAlias != this.ManufacturerAlias;
        }

        public DeviceNotifyInfomation DeviceInfo { get; private set; }
        public List<DeviceNotifyInfomation> Root {  get; private set; }

        public string ManufacturerAlias { get; set; }
        public string DeviceNameAlias { get; set; }
    }
}
