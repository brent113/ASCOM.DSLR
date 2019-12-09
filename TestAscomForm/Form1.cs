using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ASCOM.DSLR
{
    public partial class Form1 : Form
    {

        private ASCOM.DriverAccess.Camera driver;
        private System.Timers.Timer imageAcquisitionTimer;

        public Form1()
        {
            InitializeComponent();
            SetUIState();
            imageAcquisitionTimer = new System.Timers.Timer(250);
            imageAcquisitionTimer.Elapsed += ImageAcquisitionTimerElapsed;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (IsConnected)
                driver.Connected = false;

            Properties.Settings.Default.Save();
        }

        private void buttonChoose_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.DriverId = ASCOM.DriverAccess.Camera.Choose(Properties.Settings.Default.DriverId);
            SetUIState();
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (IsConnected)
            {
                driver.Connected = false;
            }
            else
            {
                driver = new ASCOM.DriverAccess.Camera(Properties.Settings.Default.DriverId);
                driver.Connected = true;
            }
            SetUIState();
        }

        private void SetUIState()
        {
            buttonConnect.Enabled = !string.IsNullOrEmpty(Properties.Settings.Default.DriverId);
            buttonChoose.Enabled = !IsConnected;
            buttonConnect.Text = IsConnected ? "Disconnect" : "Connect";
        }

        private bool IsConnected
        {
            get
            {
                return ((this.driver != null) && (driver.Connected == true));
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.DriverId != "ASCOM.DSLR.Camera")
            {
                Properties.Settings.Default.DriverId = ASCOM.DriverAccess.Camera.Choose("ASCOM.DSLR.Camera");
            }
            SetUIState();
        }

        private void buttonCapture_Click(object sender, EventArgs e)
        {
            if (!IsConnected) { buttonConnect_Click(null, null); }
            driver.StartExposure(5, true);
            imageAcquisitionTimer.Start();
        }

        private void ImageAcquisitionTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (driver.ImageReady)
            {
                imageAcquisitionTimer.Stop();
                showImage();
            }
        }

        private void showImage()
        {
            int[,] rawimg = (int[,])driver.ImageArray;
            int X = driver.CameraXSize;
            int Y = driver.CameraYSize;

            Bitmap bitmap;
            unsafe
            {
                fixed (int* intPtr = &rawimg[0, 0])
                {
                    bitmap = new Bitmap(3*X, 3*Y, X, PixelFormat.Format48bppRgb, new IntPtr(intPtr));
                }
            }
            //bitmap.Save(@"C:\out.bmp");

            pictureBox1.Image = bitmap;
        }
        
        private void buttonReload_Click(object sender, EventArgs e)
        {
            showImage();
        }
    }
}
