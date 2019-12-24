using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ASCOM.Utilities;
using ASCOM.DSLR;
using ASCOM.DeviceInterface;
using ASCOM.DSLR.Classes;
using ASCOM.DSLR.Enums;
using System.IO;
using System.Linq;
using System.Threading;
using System.IO.Ports;

namespace ASCOM.DSLR
{
    [ComVisible(false)]
    public partial class SetupDialogForm : Form
    {
        public SetupDialogForm(CameraSettings settings)
        {
            Settings = settings;
            InitializeComponent();
            InitUI();
        }

        private void CmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            Settings.TraceLog = chkTrace.Checked;
            Settings.CameraMode = (CameraMode)cbImageMode.SelectedItem;


            Settings.IntegrationApi = (ConnectionMethod)cbIntegrationApi.SelectedItem;

            if (Directory.Exists(tbSavePath.Text))
            {
                Settings.StorePath = tbSavePath.Text;
            }
            Settings.Iso = (short)cbIso.SelectedValue;

            Settings.EnableBinning = chkEnableBin.Checked;
            Settings.BinningMode = (BinningMode)cbBinningMode.SelectedItem;
        }

        private void CmdCancel_Click(object sender, EventArgs e) // Cancel button event handler
        {
            Close();
        }

        private void BrowseToAscom(object sender, EventArgs e) // Click on ASCOM logo event handler
        {
            try
            {
                System.Diagnostics.Process.Start("http://ascom-standards.org/");
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (System.Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        public CameraSettings Settings { get; private set; }

        private void SetSelectedItem(ComboBox comboBox, object selectedItem)
        {
            if (comboBox.Items.Contains(selectedItem))
            {
                comboBox.SelectedItem = selectedItem;
            }
        }

        private void InitUI()
        {
            chkTrace.Checked = Settings.TraceLog;

            cbImageMode.Items.Clear();
            cbImageMode.Items.Add(CameraMode.RGGB);
            cbImageMode.Items.Add(CameraMode.Color16);
            cbImageMode.Items.Add(CameraMode.ColorJpg);
            SetSelectedItem(cbImageMode, Settings.CameraMode);

            chkEnableBin.Checked = Settings.EnableBinning;

            cbIntegrationApi.Items.Add(ConnectionMethod.Nikon);
            SetSelectedItem(cbIntegrationApi, Settings.IntegrationApi);


            cbBinningMode.Items.Add(BinningMode.Sum);
            cbBinningMode.Items.Add(BinningMode.Median);
            SetSelectedItem(cbBinningMode, Settings.BinningMode);

            var isoValues = ISOValues.Values.Where(v => v.DoubleValue <= short.MaxValue && v.DoubleValue > 0).Select(v => (short)v.DoubleValue);
            cbIso.DisplayMember = "display";
            cbIso.ValueMember = "value";
            cbIso.DataSource = isoValues.Select(v => new { value = v, display = v.ToString() }).ToArray();
            cbIso.SelectedValue = Settings.Iso;

            tbSavePath.Text = Settings.StorePath;
            
            UpdateUiState();

        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(tbSavePath.Text))
            {
                folderBrowserDialog.SelectedPath = tbSavePath.Text;
            }

            var thread = new Thread(new ParameterizedThreadStart(param =>
            {
                this.Invoke((Action)delegate
                {
                    if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        tbSavePath.Text = folderBrowserDialog.SelectedPath;
                    }
                });
            }));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void TbBackyardEosPort_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void ChkEnableBin_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUiState();
        }

        private void EnableBinChanged()
        {
            if (chkEnableBin.Checked)
            {
                SetSelectedItem(cbImageMode, CameraMode.RGGB);

                cbImageMode.Visible = false;
                lbImageMode.Visible = false;

                cbBinningMode.Enabled = true;
            }
            else
            {
                cbImageMode.Visible = true;
                lbImageMode.Visible = true;

                cbBinningMode.Enabled = false;
            }
        }

        private void LblBinningMode_Click(object sender, EventArgs e)
        {

        }

        private void CbBinningMode_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void CbIntegrationApi_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUiState();
        }

        private void UpdateUiState()
        {
            ConnectionMethodChanged();
            EnableBinChanged();
        }

        private void ConnectionMethodChanged()
        {
            bool isDigiCamControl = IsDigiCamControl();
        }

        private bool IsDigiCamControl()
        {
            return cbIntegrationApi.SelectedItem != null && (ConnectionMethod)cbIntegrationApi.SelectedItem == ConnectionMethod.Nikon;
        }

        private void CbImageMode_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void ChkEnableLiveView_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUiState();
        }

        private void BtnAbout_Click(object sender, EventArgs e)
        {
            var aboutForm = new About();
            aboutForm.ShowDialog(this);
        }
    }
}