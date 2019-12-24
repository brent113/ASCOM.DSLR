using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace ASCOM.DSLR
{
    public partial class Form1 : Form
    {

        private ASCOM.DriverAccess.Camera driver;
        private Timer imageAcquisitionTimer;
        private Timer connectedTimer;

        public Form1()
        {
            InitializeComponent();
            SetUIState();
            imageAcquisitionTimer = new Timer(250);
            imageAcquisitionTimer.Elapsed += ImageAcquisitionTimerElapsed;
            connectedTimer = new Timer(250);
            connectedTimer.Elapsed += ConnectedTimerElapsed;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (IsConnected)
                driver.Connected = false;

            Properties.Settings.Default.Save();
        }

        private void ButtonChoose_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.DriverId = ASCOM.DriverAccess.Camera.Choose(Properties.Settings.Default.DriverId);
            SetUIState();
        }

        private void ButtonConnect_Click(object sender, EventArgs e)
        {
            if (IsConnected)
            {
                driver.Connected = false;
            }
            else
            {
                driver = new ASCOM.DriverAccess.Camera(Properties.Settings.Default.DriverId);
                try
                {
                    driver.Connected = true;
                }
                catch (ASCOM.NotConnectedException)
                {
                    driver.SetupDialog();
                }

                connectedTimer.Start();
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

        private void ButtonCapture_Click(object sender, EventArgs e)
        {
            int count = 0;
            while (!IsConnected && count < 2) { ButtonConnect_Click(null, null); count++; }
            driver.StartExposure(2.75, true);
            imageAcquisitionTimer.Start();
        }

        private void ImageAcquisitionTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            imageAcquisitionTimer.Stop();
            try
            {
                if (driver.ImageReady)
                {
                    ShowImage();
                }
                else
                {
                    imageAcquisitionTimer.Start();
                }
            }
            catch { }
        }
        private void ConnectedTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            SetUIState();
        }

        private void ShowImage()
        {
            if (driver == null)
                return;

            int[,] rawimg = (int[,])driver.ImageArray;
            int X = driver.CameraXSize / 2;
            int Y = driver.CameraYSize / 2;
            Rectangle re = new Rectangle(0, 0, X, Y);

            // Prescale
            int[] img = new int[X * Y];
            for (int j = 0; j < Y; j++)
            {
                for (int i = 0; i < X; i++)
                {
                    // Monochrome
                    //img[j * X + i] = (byte)(rawimg[i, j] >> 6); // 14 -> 8
                    //img[j * X + i] = img[j * X + i] | img[j * X + i] << 8 | img[j * X + i] << 16;

                    // Prescaling
                    int R = rawimg[2 * i + 0, 2 * j + 0];
                    int G1 = rawimg[2 * i + 1, 2 * j + 0];
                    int G2 = rawimg[2 * i + 0, 2 * j + 1];
                    int B = rawimg[2 * i + 1, 2 * j + 1];

                    // Gamma
                    double GGamma = 1 / 1.4, GScale = 127 * .82;
                    R = (int)(Math.Pow(R / 16383.0, 1 / 2.3) * 235);
                    G1 = (int)(Math.Pow(G1 / 16383.0, GGamma) * GScale);
                    G2 = (int)(Math.Pow(G2 / 16383.0, GGamma) * GScale);
                    B = (int)(Math.Pow(B / 16383.0, 1 / 1.6) * 255);

                    img[j * X + i] = (byte)R << 16 | (byte)(G1 + G2) << 8 | (byte)B;
                }
            }

            Bitmap bitmap;
            bitmap = new Bitmap(X, Y, PixelFormat.Format32bppRgb);
            BitmapData BmpData = bitmap.LockBits(re, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(img, 0, BmpData.Scan0, img.Length);
            bitmap.UnlockBits(BmpData);

            pictureBox1.Image = bitmap;
        }

        private void ButtonRedraw_Click(object sender, EventArgs e)
        {
            ShowImage();
        }
    }
}
