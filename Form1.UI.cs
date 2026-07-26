using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace 인하테크개조
{
    public partial class Form1 : Form
    {
                private void ResetJudgeLamp()
                {
                    lblOk.BackColor = Color.Gray;
                    lblNg.BackColor = Color.Gray;
                }

                private void SetJudgeLamp(bool ok)
                {
                    lblOk.BackColor = ok ? Color.Lime : Color.Gray;
                    lblNg.BackColor = ok ? Color.Gray : Color.Red;
                    lblOk.Refresh();
                    lblNg.Refresh();
                }

                private void SetAutoManualLamp(bool autoMode)
                {
                    lblAuto.BackColor = autoMode ? Color.Lime : Color.Gray;
                    lblManual.BackColor = autoMode ? Color.Gray : Color.Yellow;
                    lblAuto.Refresh();
                    lblManual.Refresh();
                }

                private void SetCommunicationLamp(bool connected)
                {
                    lblResponseOn.BackColor = connected ? Color.Lime : Color.Gray;
                    lblResponseOff.BackColor = connected ? Color.Gray : Color.Red;
                    lblResponseOn.Refresh();
                    lblResponseOff.Refresh();
                }

                private async Task ShowAutoCloseMessageAsync(
                    string message, int milliseconds)
                {
                    Form popup = new Form
                    {
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        StartPosition = FormStartPosition.CenterParent,
                        ClientSize = new Size(260, 100),
                        ControlBox = false,
                        ShowInTaskbar = false,
                        TopMost = true
                    };
        
                    popup.Controls.Add(new Label
                    {
                        Dock = DockStyle.Fill,
                        Text = message,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("맑은 고딕", 14F, FontStyle.Bold)
                    });
        
                    popup.Show(this);
                    await Task.Delay(milliseconds);
        
                    if (!popup.IsDisposed)
                        popup.Close();
        
                    popup.Dispose();
                }
    }
}
