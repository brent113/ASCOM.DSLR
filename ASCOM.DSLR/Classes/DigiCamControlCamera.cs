using ASCOM.DeviceInterface;
using ASCOM.DSLR.Enums;
using ASCOM.Utilities;
using CameraControl.Devices;
using CameraControl.Devices.Classes;
using CameraControl.Devices.Wifi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ASCOM.DSLR.Classes
{
    public class DigiCamControlCamera : BaseCamera
    {
        public string Model
        {
            get
            {
                string model = string.Empty;
                if (_cameraModel != null)
                {
                    model = _cameraModel.Name;
                }

                return model;
            }
        }


        public CameraDeviceManager DeviceManager { get; set; }

        public ConnectionMethod IntegrationApi => ConnectionMethod.Nikon;

        public bool SupportsViewView { get { return false; } }

        public CameraStates CameraState { get; set; }

        public event EventHandler<ImageReadyEventArgs> ImageReady;
#pragma warning disable CS0067
        public event EventHandler<ExposureFailedEventArgs> ExposureFailed;
        public event EventHandler<LiveViewImageReadyEventArgs> LiveViewImageReady;
#pragma warning restore CS0067

        private TraceLogger _tl;

        private bool isConnected;

        public DigiCamControlCamera(TraceLogger tl, List<CameraModel> cameraModelHistory) : base(cameraModelHistory)
        {
            _tl = tl;
            DeviceManager = new CameraDeviceManager
            {
                LoadWiaDevices = false,
                DetectWebcams = false
            };
            DeviceManager.CameraSelected += DeviceManager_CameraSelected;
            DeviceManager.CameraConnected += DeviceManager_CameraConnected;
            DeviceManager.PhotoCaptured += DeviceManager_PhotoCaptured;
            DeviceManager.CameraDisconnected += DeviceManager_CameraDisconnected;

            Log.LogError += Log_LogError;
            Log.LogDebug += Log_LogError;
            Log.LogInfo += Log_LogError;

        }

        public void AbortExposure()
        {
            _canceled.IsCanceled = true;
        }

        private void LogProperties(PropertyValue<long> properties)
        {
            try
            {
                StringBuilder propsStr = new StringBuilder(properties.Name + ": ");
                foreach (var p in properties.Values)
                {
                    propsStr.Append(p);
                    propsStr.Append(":");
                }
                _tl.LogMessage("Property values", propsStr.ToString());
            }
            catch { }
        }

        public void ConnectCamera()
        {
            _ConnectCamera(true);
        }

        public void _ConnectCamera(bool validatePropertiesDatabase)
        {
            DeviceManager.ConnectToCamera();
            var camera = DeviceManager.SelectedCameraDevice;
            LogCameraInfo(camera);

            if (camera.DeviceName == null)
            {
                _tl.LogMessage("Nikon ConnectCamera", "No camera connected");
                throw new NotConnectedException();
            }

            // If there's not a valid model in the database, require properties to be displayed by throwing an error.
            var cameraModel = _cameraModelsHistory.FirstOrDefault(c => c.Name == camera.DeviceName); //try get sensor params from history
            if (validatePropertiesDatabase && cameraModel == null)
            {
                _tl.LogMessage("Nikon ConnectCamera", "Properties not set, visit properties dialog first.");
                throw new NotConnectedException();
            }
        }

        private void LogCameraInfo(ICameraDevice camera)
        {
            _tl.LogMessage("DeviceName", camera.DeviceName);
            LogProperties(camera.IsoNumber);
            LogProperties(camera.ShutterSpeed);
            LogProperties(camera.FNumber);
            LogProperties(camera.Mode);
            LogProperties(camera.FocusMode);
            LogProperties(camera.CompressionSetting);

            foreach (var p in camera.Properties)
            {
                LogProperties(p);
            }

            foreach (var p in camera.AdvancedProperties)
            {
                LogProperties(p);
            }
        }

        public void DisconnectCamera()
        {
            foreach (var device in DeviceManager.ConnectedDevices.ToList())
            {
                DeviceManager.DisconnectCamera(device);
            }
        }

        public void Dispose()
        {
            DisconnectCamera();
        }

        public override CameraModel ScanCameras()
        {
            // Don't return the default fake device without connecting to something first.
            if (DeviceManager.ConnectedDevices.Count == 0) { _ConnectCamera(false); }
            if (DeviceManager.ConnectedDevices.Count == 0) { throw new NotConnectedException("Make sure the camera is connected and powered on."); }

            var cameraDevice = DeviceManager.SelectedCameraDevice;
            var cameraModel = GetCameraModel(cameraDevice.DeviceName);

            var _cameraModel = _cameraModelsHistory.FirstOrDefault(c => c.Name == cameraModel.Name); //try get sensor params from history
            if (_cameraModel == null)
            {
                _cameraModel = new CameraModel
                {
                    Name = cameraModel.Name
                };
            }
            _cameraModel.ImageHeight = cameraModel.ImageHeight;
            _cameraModel.ImageWidth = cameraModel.ImageWidth;
            _cameraModel.SensorHeight = cameraModel.SensorHeight;
            _cameraModel.SensorWidth = cameraModel.SensorWidth;
            return _cameraModel;
        }

        private double ParseValue(string valueStr)
        {
            valueStr = valueStr.Replace(',', '.');
            if (!double.TryParse(valueStr, out double value))
            {
                if (valueStr.Contains("/"))
                {
                    value = ParseValue(valueStr.Split('/').Last());
                    if (value > 0)
                    {
                        value = 1 / value;
                    }
                }
                else if (valueStr.EndsWith("s"))
                {
                    double.TryParse(valueStr.TrimEnd('s'), out value);
                }
            }

            return value;
        }

        private string GetNearesetValue(PropertyValue<long> propertyValue, double value)
        {
            try
            {
                string nearest = propertyValue.Values.Select(v =>
                {

                    double doubleValue = ParseValue(v);
                    return new
                    {
                        ValueStr = v,
                        DoubleValue = doubleValue,
                        Difference = Math.Abs(doubleValue - value)
                    };
                }).Where(i => i.DoubleValue > 0).OrderBy(i => i.Difference).First().ValueStr;

                return nearest;
            }
            catch
            {
                throw new DriverException();
            }
        }

        private string GetNearesetShutter(PropertyValue<long> propertyValue, double value)
        {
            string nearest = propertyValue.Values.Select(v =>
            {

                double doubleValue = ParseValue(v);
                return new
                {
                    ValueStr = v,
                    DoubleValue = doubleValue,
                    Difference = Math.Abs(doubleValue - value)
                };
            }).Where(i => i.DoubleValue > 0).OrderBy(i => i.Difference).First().ValueStr;

            return nearest;
        }


        DigiCamCanceledFlag _canceled = new DigiCamCanceledFlag();
        private double _duration;
        private DateTime _startTime;

        public async void StartExposure(double Duration, bool Light)
        {
            _canceled.IsCanceled = false;

            _startTime = DateTime.Now;
            _duration = Duration;
            var camera = DeviceManager.SelectedCameraDevice;
            if (camera.DeviceName == null)
            {
                throw new NotConnectedException();
            }
            camera.IsoNumber.Value = GetNearesetValue(camera.IsoNumber, Iso);
            camera.CompressionSetting.Value = camera.CompressionSetting.Values.SingleOrDefault(v => v.ToUpper() == "RAW");
            bool canBulb = camera.GetCapability(CapabilityEnum.Bulb);
            if (Duration > 30)
            {
                int durationMsec = (int)(Duration * 1000);
                await Task.Run(() => BulbExposure(durationMsec, _canceled, camera.StartBulbMode, camera.EndBulbMode));
            }
            else
            {
                camera.ShutterSpeed.Value = GetNearesetShutter(camera.ShutterSpeed, Duration);
                DeviceManager.SelectedCameraDevice.CapturePhotoNoAf();
            }
        }


        private void BulbExposure(int bulbTime, DigiCamCanceledFlag canceledFlag, Action startBulb, Action endBulb)
        {
            startBulb();

            int seconds = bulbTime / 1000;
            int milliseconds = bulbTime % 1000;

            Thread.Sleep(milliseconds);
            for (int i = 1; i <= seconds; i++)
            {
                Thread.Sleep(1000);
                if (canceledFlag.IsCanceled)
                {
                    canceledFlag.IsCanceled = false;
                    break;
                }
            }

            endBulb();
        }

        public void StopExposure()
        {
            AbortExposure();
        }

        private void Log_LogError(LogEventArgs e)
        {
            try
            {
                _tl.LogMessage(e.Message.ToString(), e.Exception?.Message);
            }
            catch { }
        }

        private void PhotoCaptured(PhotoCapturedEventArgs eventArgs)
        {
            _tl.LogMessage("Photo captured filename", eventArgs.FileName);

            string fileName = GetFileNameForDownload(eventArgs);
            if (!Directory.Exists(Path.GetDirectoryName(fileName)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            }

            eventArgs.CameraDevice.TransferFile(eventArgs.Handle, fileName);

            SensorTemperature = GetSensorTemperature(fileName);

            string newFilePath = RenameFile(fileName, _duration, _startTime);
            ImageReady?.Invoke(this, new ImageReadyEventArgs(newFilePath));

            eventArgs.CameraDevice.IsBusy = false;
        }

        private string GetFileNameForDownload(PhotoCapturedEventArgs eventArgs)
        {
            string fileName = Path.Combine(StorePath, Path.GetFileName(eventArgs.FileName));
            if (File.Exists(fileName))
                fileName =
                  StaticHelper.GetUniqueFilename(
                    Path.GetDirectoryName(fileName) + "\\" + Path.GetFileNameWithoutExtension(fileName) + "_", 0,
                    Path.GetExtension(fileName));

            if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
            {
                fileName = Path.ChangeExtension(fileName, "nef");
            }

            return fileName;
        }

        void DeviceManager_PhotoCaptured(object sender, PhotoCapturedEventArgs eventArgs)
        {
            PhotoCaptured(eventArgs);
        }

        void DeviceManager_CameraConnected(ICameraDevice cameraDevice)
        {
            isConnected = true;
            CameraState = CameraStates.cameraIdle;
        }

        void DeviceManager_CameraDisconnected(ICameraDevice cameraDevice)
        {
            //DeviceManager.CloseAll();
            isConnected = false;

            CameraState = CameraStates.cameraError;
        }

        void DeviceManager_CameraSelected(ICameraDevice oldcameraDevice, ICameraDevice newcameraDevice)
        {
        }

        public bool IsConnected()
        {
            return isConnected;
        }
    }


    public class DigiCamCanceledFlag
    {
        public bool IsCanceled = false;
    }
}
