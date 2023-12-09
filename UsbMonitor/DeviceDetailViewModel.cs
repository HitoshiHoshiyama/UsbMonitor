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
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public DeviceNotifyInfomation DeviceInfo { get; private set; }
        public List<DeviceNotifyInfomation> Root {  get; private set; }
    }
}
