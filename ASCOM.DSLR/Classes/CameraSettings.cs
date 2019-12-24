using ASCOM.DSLR.Enums;
using System;
using System.Collections.Generic;
using System.IO;

namespace ASCOM.DSLR.Classes
{
    [Serializable]
    public class CameraSettings
    {
        public CameraSettings()
        {
            TraceLog = true;
            CameraMode = CameraMode.RGGB;
            StorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ASCOM_DSLR");
            IntegrationApi = ConnectionMethod.Nikon;
            //IntegrationApi = ConnectionMethod.CanonSdk;
            Iso = 400;
            EnableBinning = false;
            BinningMode = BinningMode.Sum;
            CameraModelsHistory = new List<CameraModel>();
        }

        public bool TraceLog { get; set; }

        public CameraMode CameraMode { get; set; }

        public ConnectionMethod IntegrationApi { get; set; }

        public string StorePath { get; set; }

        public short Iso { get; set; }

        public bool EnableBinning { get; set; }

        public BinningMode BinningMode { get; set; }

        public List<CameraModel> CameraModelsHistory { get; set; }
    }
}
