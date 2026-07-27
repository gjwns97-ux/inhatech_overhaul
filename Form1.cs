using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

namespace 인하테크개조
{
    public partial class Form1 : System.Windows.Forms.Form
    {
        private bool modelEditOpening = false;

        private readonly XgtPlcClient plc =
        new XgtPlcClient();
        public Form1()
        {


            InitializeComponent();
            InitializeProgramLogic();
        }



        private void btnPlcConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (plc.IsConnected)
                {
                    plc.Disconnect();

                    btnPlcConnect.Text = "PLC 연결";

                    MessageBox.Show(
                        "PLC 연결을 해제했습니다.");

                    return;
                }

                // XBL-EMTA 실제 IP로 변경
                string plcIp = "192.168.1.2";

                plc.Connect(plcIp, 2004);

                btnPlcConnect.Text = "PLC 해제";

                MessageBox.Show(
                    "XBL-EMTA TCP 2004 연결 성공");
            }
            catch (Exception ex)
            {
                plc.Disconnect();

                btnPlcConnect.Text = "PLC 연결";

                MessageBox.Show(
                    "PLC 연결 실패\r\n\r\n" +
                    ex.Message);
            }
        }

        private void btnQtyR_Click(object sender, EventArgs e)
        {
            if (!plcConnected)
                return;

            plc.WriteWord(ADDR_TOTAL_QTY, 0);

            CurrentQty.TotalQty = 0;
            UpdateQtyLabel();
        }

        private void btnPqtyR_Click(object sender, EventArgs e)
        {
            CurrentQty.PassQty = 0;
            UpdateQtyLabel();
            SaveQtySettings();
        }

        private void btnNqtyR_Click(object sender, EventArgs e)
        {
            CurrentQty.NgQty = 0;
            UpdateQtyLabel();
            SaveQtySettings();
        }
    }
}
    