using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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
            Rectangle R = new Rectangle(0, 0, X, Y);

            // Prescale
            int[] img = new int[X * Y];
            for (int j = 0; j < Y; j++)
            {
                for (int i = 0; i < X; i++)
                {
                    img[j * X + i] = (byte)(rawimg[i, j] >> 6); // 14 -> 8
                    img[j * X + i] = img[j * X + i] | img[j * X + i] << 8 | img[j * X + i] << 16;
                }
            }

            Bitmap bitmap;
            bitmap = new Bitmap(X, Y, PixelFormat.Format32bppRgb);
            BitmapData BmpData = bitmap.LockBits(R, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(img, 0, BmpData.Scan0, img.Length);
            bitmap.UnlockBits(BmpData);

            pictureBox1.Image = bitmap;
        }
    }
}
