using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using CyBLE_MTK_Application;
using CypressSemiconductor.ChinaManufacturingTest;
using System.IO;

namespace CurrentBoardControl
{
    public partial class MainForm : Form
    {
        private CheckBox[] ChkBoxes = new CheckBox[8];
        private Label[] Labels = new Label[8];
        private byte ChMask = 0;
        private LogManager Log;
        public MainForm()
        {
            InitializeComponent();
            ChkBoxes[0] = checkBox1;
            ChkBoxes[1] = checkBox2;
            ChkBoxes[2] = checkBox3;
            ChkBoxes[3] = checkBox4;
            ChkBoxes[4] = checkBox5;
            ChkBoxes[5] = checkBox6;
            ChkBoxes[6] = checkBox7;
            ChkBoxes[7] = checkBox8;
            Labels[0] = label1;
            Labels[1] = label2;
            Labels[2] = label3;
            Labels[3] = label4;
            Labels[4] = label5;
            Labels[5] = label6;
            Labels[6] = label7;
            Labels[7] = label8;
            Log = new LogManager(LogDetailLevel.LogEverything, LogTextBox);
            MTKCurrentMeasureBoard.Board.Log = Log;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (MTKCurrentMeasureBoard.Board.Connected())
            {
                BtnOpen.Text = "Close";
                BtnMeasure.Enabled = true;
                for (int i = 0; i < 8; i++)
                {
                    ChkBoxes[i].Enabled = true;
                    Labels[i].Text = "-";
                }
                checkBoxAll.Enabled = true;
            }
            else
            {
                BtnOpen.Text = "Open";
                BtnMeasure.Enabled = false;
                for (int i = 0; i < 8; i++)
                {
                    ChkBoxes[i].Checked = false;
                    ChkBoxes[i].Enabled = false;
                    Labels[i].Text = "";
                    ChMask = 0;
                }
                checkBoxAll.Checked = false;
                checkBoxAll.Enabled = false;
            }
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            if (MTKCurrentMeasureBoard.Board.Connected())
            {
                MTKCurrentMeasureBoard.Board.SPort.Close();
                
                RefreshStatus();

                return;
            }

            SerialPort SPort = new SerialPort();
            if (!ComPortInfoList[SerialPortCombo.SelectedIndex].IsDummyPort)
                SPort.PortName = ComPortInfoList[SerialPortCombo.SelectedIndex].Name;
            SPort.Handshake = Handshake.RequestToSend;
            SPort.BaudRate = 115200;
            SPort.WriteTimeout = 1000;
            SPort.ReadTimeout = 1000;
            try
            {
                SPort.Open();
                List<string> Rets;
                if (HCISupport.Who(SPort, Log, this, out Rets))
                {
                    if (Rets[0] == "HOST" &&
                        Rets[1] == "819")
                    {
                        MTKCurrentMeasureBoard.Board.Connect(SPort);
                        RefreshStatus();
                        return;
                    }
                }
                SPort.Close();
                MessageBox.Show(SPort.PortName + " - is not MTK-CURRENT-MEASURE-BOARD.",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                MessageBox.Show(SPort.PortName + " - is in use.",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            RefreshStatus();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (!MTKCurrentMeasureBoard.Board.Connected())
                return;

            byte chmask = 0;
            for (int i = 0; i < 8; i++)
            {
                if (ChkBoxes[i].Enabled && ChkBoxes[i].Checked)
                {
                    chmask |= (byte)(1 << i);
                }
            }
            if (chmask != ChMask)
            {
                if (!MTKCurrentMeasureBoard.Board.SW.SetRelayWellA((byte)(chmask & 0xf), (byte)((chmask & 0xF0) >> 4)))
                {
                    MessageBox.Show("SetRelayWellA FAIL", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    ChMask = chmask;
                }
            }
        }

        private void BtnMeasure_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 8; i++)
            {
                if (ChkBoxes[i].Enabled && ChkBoxes[i].Checked)
                {
                    Current cur = MTKCurrentMeasureBoard.Board.DMM.MeasureCurrent(i);
                    Labels[i].Text = cur.average.ToString("F02") + " mA";
                }
                else 
                {
                    Labels[i].Text = "-";
                }
            }
        }
        static private List<COMPortInfo> ComPortInfoList;
        private void AddPorts()
        {
            //AHMI: To speed up startup.
            if (ComPortInfoList == null || ComPortInfoList.Count == 0)
                ComPortInfoList = COMPortInfo.GetCOMPortsInfo();
            Graphics ComboGraphics = SerialPortCombo.CreateGraphics();
            Font ComboFont = SerialPortCombo.Font;
            int MaxWidth = 0;
            foreach (COMPortInfo ComPort in ComPortInfoList)
            {
                string s = ComPort.Name + " - " + ComPort.Description;
                SerialPortCombo.Items.Add(s);
                int VertScrollBarWidth = (SerialPortCombo.Items.Count > SerialPortCombo.MaxDropDownItems) ? SystemInformation.VerticalScrollBarWidth : 0;
                int DropDownWidth = (int)ComboGraphics.MeasureString(s, ComboFont).Width + VertScrollBarWidth;
                if (MaxWidth < DropDownWidth)
                {
                    SerialPortCombo.DropDownWidth = DropDownWidth;
                    MaxWidth = DropDownWidth;
                }
            }
            if (SerialPortCombo.Items.Count > 0)
            {
                SerialPortCombo.SelectedIndex = 0;
            }
            Log.PrintLog(this, ComPortInfoList.Count.ToString() + " serial ports found.", LogDetailLevel.LogRelevant);
        }

        private void RefreshPortList(bool RescanPorts = true)
        {
            Log.PrintLog(this, "Removing all serial ports.", LogDetailLevel.LogEverything);
            SerialPortCombo.Items.Clear();
            Log.PrintLog(this, "Rediscovering serial ports.", LogDetailLevel.LogEverything);
            if (RescanPorts && ComPortInfoList != null)
                ComPortInfoList.Clear();
            AddPorts();
        }


        private void MainForm_Load(object sender, EventArgs e)
        {
            RefreshPortList();
        }

        private void checkBoxAll_CheckedChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < 8; i++)
            {
                ChkBoxes[i].Checked = checkBoxAll.Checked;
            }
        }
    }
}
