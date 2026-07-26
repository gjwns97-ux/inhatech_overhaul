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

        private void btnReadTest_Click(object sender, EventArgs e)
        {
            try
            {
                // 연결 안 되어 있으면 연결
                if (!plc.IsConnected)
                    plc.Connect("192.168.1.2", 2004, 2000);

                // D1000 읽기
                ushort value = plc.ReadWord("D2000");

                MessageBox.Show(
                    $"통신 성공!\n\n" +
                    $"D2000 = {value}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "통신 실패\n\n" +
                    ex.Message);
            }
        }

        private void btnWriteTest_Click(object sender, EventArgs e)
        {
            try
            {
                // 연결 안 되어 있으면 연결
                if (!plc.IsConnected)
                    plc.Connect("192.168.1.2", 2004, 2000);

                // 테스트 값 쓰기
                plc.WriteWord("D1000", 1111);
                plc.WriteWord("D1002", 2222);
                plc.WriteWord("D1004", 3333);
                plc.WriteWord("D1006", 4444);
                plc.WriteWord("D1008", 5555);

                MessageBox.Show("쓰기 완료!");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "쓰기 실패\n\n" +
                    ex.ToString());
            }
        }
    }
}
