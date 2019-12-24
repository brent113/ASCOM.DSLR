using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CameraControl.Devices.Classes;
using PortableDeviceLib;
using PortableDeviceLib.Model;

namespace CameraControl.Devices.Nikon
{
    public class NikonD810 : NikonD600
    {
        public NikonD810()
        {
            _isoTable = new Dictionary<uint, string>()
            {
                {0x0020, "Lo 1.0"},
                {0x0028, "Lo 0.7"},
                {0x002d, "Lo 0.5"},
                {0x0032, "Lo 0.3"},
                {0x0040, "64"},
                {0x0048, "72"},
                {0x0050, "80"},
                {0x0064, "100"},
                {0x007D, "125"},
                {0x00A0, "160"},
                {0x00C8, "200"},
                {0x00FA, "250"},
                {0x0140, "320"},
                {0x0190, "400"},
                {0x01F4, "500"},
                {0x0280, "640"},
                {0x0320, "800"},
                {0x03E8, "1000"},
                {0x04E2, "1250"},
                {0x0640, "1600"},
                {0x07D0, "2000"},
                {0x09C4, "2500"},
                {0x0C80, "3200"},
                {0x0FA0, "4000"},
                {0x1388, "5000"},
                {0x1900, "6400"},
                {0x1F40, "8000"},
                {0x2710, "10000"},
                {0x3200, "12800"},
                {0x3e80, "Hi 0.3"},
                {0x4650, "Hi 0.5"},
                {0x4e20, "Hi 0.7"},
                {0x6400, "Hi 1.0"},
                {0xC800, "Hi 2.0"},
            };

            _autoIsoTable = new Dictionary<byte, string>()
            {
                {0, "72"},
                {1, "80"},
                {2, "100"},
                {3, "125"},
                {4, "140"},
                {5, "160"},
                {6, "200"},
                {7, "250"},
                {8, "280"},
                {9, "320"},
                {10, "400"},
                {11, "500"},
                {12, "560"},
                {13, "640"},
                {14, "800"},
                {15, "1000"},
                {16, "1100"},
                {17, "1250"},
                {18, "1600"},
                {19, "2000"},
                {20, "2200"},
                {21, "2500"},
                {22, "3200"},
                {23, "4000"},
                {24, "4500"},
                {25, "5000"},
                {26, "6400"},
                {27, "8000"},
                {28, "9000"},
                {29, "10000"},
                {30, "12800"},
                {31, "Hi 0.3"},
                {32, "Hi 0.5"},
                {33, "Hi 0.7"},
                {34, "Hi 1"},
                {35, "Hi 2"},
            };
        }

        byte oldval = 0;

        protected override void InitIso()
        {
            lock (Locker)
            {
                NormalIsoNumber = new PropertyValue<long>();
                NormalIsoNumber.Name = "IsoNumber";
                NormalIsoNumber.SubType = typeof(int);
                NormalIsoNumber.ValueChanged += IsoNumber_ValueChanged;
                NormalIsoNumber.Clear();
                try
                {
                    DeviceReady();
                    MTPDataResponse result = ExecuteReadDataEx(CONST_CMD_GetDevicePropDesc, CONST_PROP_ExposureIndex);
                    //IsoNumber.IsEnabled = result.Data[4] == 1;
                    UInt16 defval = BitConverter.ToUInt16(result.Data, 7);
                    for (int i = 0; i < result.Data.Length - 12; i += 2)
                    {
                        UInt16 val = BitConverter.ToUInt16(result.Data, 12 + i);
                        NormalIsoNumber.AddValues(_isoTable.ContainsKey(val) ? _isoTable[val] : val.ToString(), val);
                    }
                    NormalIsoNumber.ReloadValues();
                    NormalIsoNumber.SetValue(defval, false);
                    IsoNumber = NormalIsoNumber;
                }
                catch (Exception)
                {
                    NormalIsoNumber.IsEnabled = false;
                }

                MovieIsoNumber = new PropertyValue<long>();
                MovieIsoNumber.Name = "IsoNumber";
                MovieIsoNumber.SubType = typeof(int);
                MovieIsoNumber.ValueChanged += MovieIsoNumber_ValueChanged;
                MovieIsoNumber.Clear();
                try
                {
                    MTPDataResponse result = ExecuteReadDataEx(CONST_CMD_GetDevicePropDesc, CONST_PROP_MovieExposureIndex);
                    //IsoNumber.IsEnabled = result.Data[4] == 1;
                    UInt16 defval = BitConverter.ToUInt16(result.Data, 7);
                    for (int i = 0; i < result.Data.Length - 12; i += 2)
                    {
                        UInt16 val = BitConverter.ToUInt16(result.Data, 12 + i);
                        MovieIsoNumber.AddValues(_isoTable.ContainsKey(val) ? _isoTable[val] : val.ToString(CultureInfo.InvariantCulture), val);
                    }
                    MovieIsoNumber.ReloadValues();
                    MovieIsoNumber.SetValue(defval, false);
                }
                catch (Exception)
                {
                    MovieIsoNumber.IsEnabled = false;
                }
            }
        }

        private void SlowDownEventTimer()
        {
            _timer.Interval = 1000 / 10;
        }

        private void SpeedUpEventTimer()
        {
            _timer.Interval = 1000 / 10;
        }

        public override void CapturePhotoNoAf()
        {
            MTPDataResponse val = null;
            SpeedUpEventTimer();
            lock (Locker)
            {
                byte oldval = 0;
                try
                {
                    IsBusy = true;
                    DeviceReady();
                    MTPDataResponse response = ExecuteReadDataEx(CONST_CMD_GetDevicePropValue, CONST_PROP_LiveViewStatus);
                    //ErrorCodes.GetException(response.ErrorCode);
                    // test if live view is on 
                    if (response.Data != null && response.Data.Length > 0 && response.Data[0] > 0)
                    {
                        if (CaptureInSdRam)
                        {
                            ErrorCodes.GetException(ExecuteWithNoData(CONST_CMD_InitiateCaptureRecInSdram, 0xFFFFFFFF));
                            return;
                        }
                        else
                        {
                            ErrorCodes.GetException(ExecuteWithNoData(CONST_CMD_InitiateCaptureRecInMedia, 0xFFFFFFFF, 0x0000));
                            return;
                        }
                        StopLiveView();
                    }


                    // Modified for D810 - use LockCamera before AFMode
                    LockCamera();
                    val = StillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue, CONST_PROP_AFModeSelect);
                    ErrorCodes.GetException(val.ErrorCode);
                    if (val.Data != null && val.Data.Length > 0)
                        oldval = val.Data[0];
                    SetProperty(CONST_CMD_SetDevicePropValue, new[] { (byte)4 }, CONST_PROP_AFModeSelect);

                    DeviceReady();
                    ErrorCodes.GetException(CaptureInSdRam
                                                ? ExecuteWithNoData(CONST_CMD_InitiateCaptureRecInSdram, 0xFFFFFFFF)
                                                : ExecuteWithNoData(CONST_CMD_InitiateCapture));
                    //DeviceReady();
                    //UnLockCamera(); // Perform unlock after image is read
                }
                catch
                {
                    IsBusy = false;
                    throw;
                }
            }
        }

        protected override void GetEvent(object state)
        {
            try
            {
                if (_eventIsbusy)
                    return;
                _eventIsbusy = true;
                DeviceReady(); // TODO: BRS added 12/7, was commented out previously
                MTPDataResponse response = ExecuteReadDataEx(CONST_CMD_GetEvent);

                if (response.Data == null || response.Data.Length == 0)
                {
                    Log.Debug("Get event error :" + response.ErrorCode.ToString("X"));
                    _eventIsbusy = false;
                    return;
                }
                int eventCount = BitConverter.ToInt16(response.Data, 0);
                if (eventCount > 0)
                {
                    for (int i = 0; i < eventCount; i++)
                    {
                        bool unlockReqd = false;
                        try
                        {
                            uint eventCode = BitConverter.ToUInt16(response.Data, 6 * i + 2);
                            ushort eventParam = BitConverter.ToUInt16(response.Data, 6 * i + 4);
                            int longeventParam = BitConverter.ToInt32(response.Data, 6 * i + 4);
                            switch (eventCode)
                            {
                                case CONST_Event_DevicePropChanged:
                                    ReadDeviceProperties(eventParam);
                                    break;
                                case CONST_Event_ObjectAddedInSdram:
                                case CONST_Event_ObjectAdded:
                                    {
                                        unlockReqd = true;
                                        Log.Debug("CONST_Event_ObjectAddedInSdram" + eventCode.ToString("X"));
                                        MTPDataResponse objectdata = ExecuteReadDataEx(CONST_CMD_GetObjectInfo,
                                                                                       (uint)longeventParam);
                                        string filename = "DSC_0000.JPG";
                                        if (objectdata.Data != null)
                                        {
                                            filename = Encoding.Unicode.GetString(objectdata.Data, 53, 12 * 2);
                                            if (filename.Contains("\0"))
                                                filename = filename.Split('\0')[0];
                                        }
                                        else
                                        {
                                            Log.Error("Error getting file name");
                                        }
                                        Log.Debug("File name" + filename);
                                        PhotoCapturedEventArgs args = new PhotoCapturedEventArgs
                                        {
                                            WiaImageItem = null,
                                            EventArgs =
                                                new PortableDeviceEventArgs(new PortableDeviceEventType()
                                                {
                                                    ObjectHandle = (uint)longeventParam
                                                }),
                                            CameraDevice = this,
                                            FileName = filename,
                                            Handle = (uint)longeventParam
                                        };
                                        OnPhotoCapture(this, args);
                                    }
                                    break;
                                case CONST_Event_CaptureComplete:
                                case CONST_Event_CaptureCompleteRecInSdram:
                                    {
                                        SlowDownEventTimer();
                                        OnCaptureCompleted(this, new EventArgs());
                                    }
                                    break;
                                case CONST_Event_ObsoleteEvent:
                                    break;
                                default:
                                    //Console.WriteLine("Unknown event code " + eventCode.ToString("X"));
                                    Log.Debug("Unknown event code :" + eventCode.ToString("X") + "|" +
                                              longeventParam.ToString("X"));
                                    break;
                            }
                        }
                        catch (Exception exception)
                        {
                            Log.Error("Event queue processing error ", exception);
                        }
                        finally
                        {
                            if (unlockReqd)
                            {
                                SetProperty(CONST_CMD_SetDevicePropValue, new[] { oldval }, CONST_PROP_AFModeSelect);
                                UnLockCamera();
                            }
                        }
                    }
                }
            }
            catch (InvalidComObjectException)
            {
                //return;
            }
            catch (Exception)
            {
                //Log.Error("Event exception ", exception);
            }
            _eventIsbusy = false;
            _timer.Start();
        }

        private void IsoNumber_ValueChanged(object sender, string key, long val)
        {
            lock (Locker)
            {
                SetProperty(CONST_CMD_SetDevicePropValue, BitConverter.GetBytes((int)val),
                            CONST_PROP_ExposureIndex);
            }
        }

        private void MovieIsoNumber_ValueChanged(object sender, string key, long val)
        {
            lock (Locker)
            {
                SetProperty(CONST_CMD_SetDevicePropValue, BitConverter.GetBytes((int)val),
                            CONST_PROP_MovieExposureIndex);
            }
        }
    }
}
