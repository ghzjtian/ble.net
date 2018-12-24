// Copyright M. Griffie <nexus@nexussays.com>
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Acr.UserDialogs;
using ble.net.sampleapp.util;
using nexus.core.logging;
using nexus.protocols.ble;
using nexus.protocols.ble.scan;
using Xamarin.Forms;
using System.Collections.Generic;

namespace ble.net.sampleapp.viewmodel
{
   public class BleDeviceScannerViewModel : AbstractScanViewModel
   {
      private readonly Func<BlePeripheralViewModel, Task> m_onSelectDevice;
      private DateTime m_scanStopTime;

      public BleDeviceScannerViewModel(IBluetoothLowEnergyAdapter bleAdapter, IUserDialogs dialogs,
                                        Func<BlePeripheralViewModel, Task> onSelectDevice)
         : base(bleAdapter, dialogs)
      {
         m_onSelectDevice = onSelectDevice;
         FoundDevices = new ObservableCollection<BlePeripheralViewModel>();
         ScanForDevicesCommand =
            new Command(x => { StartScan(x as Double? ?? BleSampleAppUtils.SCAN_SECONDS_DEFAULT); });
      }

      public ObservableCollection<BlePeripheralViewModel> FoundDevices { get; }

      //绑定 xml 中的 Button 按下的动作触发.
      public ICommand ScanForDevicesCommand { get; }

      public Int32 ScanTimeRemaining =>
         (Int32)BleSampleAppUtils.ClampSeconds((m_scanStopTime - DateTime.UtcNow).TotalSeconds);

      private async void StartScan(Double seconds)
      {
         if (IsScanning)
         {
            return;
         }

         if (!IsAdapterEnabled)
         {
            m_dialogs.Toast("Cannot start scan, Bluetooth is turned off");
            return;
         }

         StopScan();
         IsScanning = true;
         seconds = BleSampleAppUtils.ClampSeconds(seconds);
         m_scanCancel = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
         m_scanStopTime = DateTime.UtcNow.AddSeconds(seconds);

         Log.Trace("Beginning device scan. timeout={0} seconds", seconds);

         RaisePropertyChanged(nameof(ScanTimeRemaining));
         // RaisePropertyChanged of ScanTimeRemaining while scan is running
         Device.StartTimer(
            TimeSpan.FromSeconds(1),
            () =>
            {
               RaisePropertyChanged(nameof(ScanTimeRemaining));
               return IsScanning;
            });


         // 开始扫描附近的蓝牙 ble 设备.
         await m_bleAdapter.ScanForBroadcasts(
                // NOTE:
                //
                // You can provide a scan filter to look for particular devices. See Readme.md for more information
                // e.g.:
                //    new ScanFilter().SetAdvertisedManufacturerCompanyId( 224 /*Google*/ ),
                //
                // You can also specify additional scan settings like the amount of power to direct to the Bluetooth antenna:
                // e.g.:

                new ScanSettings()
                {
                   Mode = ScanMode.LowPower,
                   //Filter = new ScanFilter().SetAdvertisedManufacturerCompanyId(224 /*Google*/ )


                   Filter = new ScanFilter() {
                      //只扫描 固定名字 的设备,但不知道怎么扫描 一个数组名字 的设备.
                      //AdvertisedDeviceName = "Cramer",
                     

                   },
                   IgnoreRepeatBroadcasts = false
                },
            peripheral =>
            {
               Device.BeginInvokeOnMainThread(
                  () =>
                  {
                     // 扫描到设备，如果已经有了的，就更新，否则就添加.
                     var existing = FoundDevices.FirstOrDefault(d => d.Equals(peripheral));
                     if (existing != null)
                     {
                        existing.Update(peripheral);
                     }
                     else
                     {
                        System.Diagnostics.Debug.WriteLine("peripheral Name:"+peripheral.Advertisement.DeviceName);

                        FoundDevices.Add(new BlePeripheralViewModel(peripheral, m_onSelectDevice));
                     }
                  });
            },
            m_scanCancel.Token);

         IsScanning = false;
      }
   }
}
