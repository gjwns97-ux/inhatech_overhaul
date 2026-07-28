// ========================================================
// 그래프 표시 및 판정구간 표시
//
// 주요 기능
// 1. 그래프 초기화
// 2. 실시간 거리-하중 곡선 표시
// 3. 압입부/밀착부 판정박스 표시
// 4. 판정 완료 후 구간별 OK/NG 색상 표시
// ========================================================

using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace 인하테크개조
{
    public partial class Form1 : Form
    {
                private void InitPlot()
                {
                    formsPlot1.Plot.Clear();
                    AddBoxesToPlot(false);

                    // X축 제목
                    formsPlot1.Plot.XLabel("거리 [mm]");

                    // Y축 제목
                    formsPlot1.Plot.YLabel("하중 [kgf]");

            // 그래프 표시 범위 설정 
            formsPlot1.Plot.SetAxisLimits(
                    graphXMin,
                    graphXMax,
                    graphYMin,
                    graphYMax);

            // 격자선 표시
            formsPlot1.Plot.Grid(true);

                    formsPlot1.Refresh();
                }

        private void DrawPlot()
        {
            formsPlot1.Plot.Clear();           

            AddBoxesToPlot(cycleJudgeDone);

            if (servoX.Count > 1 && loadY.Count > 1)
            {
                formsPlot1.Plot.AddScatterLines(
                    servoX.ToArray(),
                    loadY.ToArray(),
                    Color.Blue,
                    2);
            }

            formsPlot1.Plot.XLabel("거리 [mm]");
            formsPlot1.Plot.YLabel("하중 [kgf]");
            formsPlot1.Plot.SetAxisLimits(
                    graphXMin,
                    graphXMax,
                    graphYMin,
                    graphYMax);
            formsPlot1.Plot.Grid(true);
            formsPlot1.Refresh();
        }

        private void btnAxisSet_Click(object sender, System.EventArgs e)
        {
            using (Form axisForm = new Form())
            {
                axisForm.Text = "그래프 축 범위 설정";
                axisForm.StartPosition = FormStartPosition.CenterParent;
                axisForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                axisForm.MaximizeBox = false;
                axisForm.MinimizeBox = false;
                axisForm.ClientSize = new Size(300, 235);

                Label lblXMin = new Label
                {
                    Text = "X축 최소",
                    Left = 20,
                    Top = 25,
                    Width = 90
                };

                TextBox txtXMin = new TextBox
                {
                    Left = 120,
                    Top = 20,
                    Width = 140,
                    Text = graphXMin.ToString("0.###")
                };

                Label lblXMax = new Label
                {
                    Text = "X축 최대",
                    Left = 20,
                    Top = 65,
                    Width = 90
                };

                TextBox txtXMax = new TextBox
                {
                    Left = 120,
                    Top = 60,
                    Width = 140,
                    Text = graphXMax.ToString("0.###")
                };

                Label lblYMin = new Label
                {
                    Text = "Y축 최소",
                    Left = 20,
                    Top = 105,
                    Width = 90
                };

                TextBox txtYMin = new TextBox
                {
                    Left = 120,
                    Top = 100,
                    Width = 140,
                    Text = graphYMin.ToString("0.###")
                };

                Label lblYMax = new Label
                {
                    Text = "Y축 최대",
                    Left = 20,
                    Top = 145,
                    Width = 90
                };

                TextBox txtYMax = new TextBox
                {
                    Left = 120,
                    Top = 140,
                    Width = 140,
                    Text = graphYMax.ToString("0.###")
                };

                Button btnOk = new Button
                {
                    Text = "적용",
                    Left = 105,
                    Top = 185,
                    Width = 90,
                    Height = 30,
                    DialogResult = DialogResult.OK
                };

                axisForm.Controls.Add(lblXMin);
                axisForm.Controls.Add(txtXMin);
                axisForm.Controls.Add(lblXMax);
                axisForm.Controls.Add(txtXMax);
                axisForm.Controls.Add(lblYMin);
                axisForm.Controls.Add(txtYMin);
                axisForm.Controls.Add(lblYMax);
                axisForm.Controls.Add(txtYMax);
                axisForm.Controls.Add(btnOk);

                axisForm.AcceptButton = btnOk;

                if (axisForm.ShowDialog(this) != DialogResult.OK)
                    return;

                double xMin;
                double xMax;
                double yMin;
                double yMax;

                if (!double.TryParse(txtXMin.Text, out xMin) ||
                    !double.TryParse(txtXMax.Text, out xMax) ||
                    !double.TryParse(txtYMin.Text, out yMin) ||
                    !double.TryParse(txtYMax.Text, out yMax))
                {
                    MessageBox.Show(
                        "축 범위를 숫자로 입력하세요.",
                        "입력 오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                if (xMin >= xMax)
                {
                    MessageBox.Show(
                        "X축 최소값은 최대값보다 작아야 합니다.",
                        "입력 오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                if (yMin >= yMax)
                {
                    MessageBox.Show(
                        "Y축 최소값은 최대값보다 작아야 합니다.",
                        "입력 오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                graphXMin = xMin;
                graphXMax = xMax;
                graphYMin = yMin;
                graphYMax = yMax;

                SaveGraphAxisSettings();

                DrawPlot();
            }
        }

        private void AddBoxesToPlot(bool showResultColor)
        {
            if (CurrentConfig == null)
                return;

            if (CurrentConfig.Boxes == null)
                return;

            for (int i = 0; i < 2; i++)
            {
                BoxSpec b = CurrentConfig.Boxes[i];

                if (!b.Use)
                    continue;

                Color color = Color.Green;

                if (showResultColor &&
                    !CheckBoxPass(servoX, loadY, b, i))
                {
                    color = Color.Red;
                }

                formsPlot1.Plot.AddLine(
                    b.PosMin, b.LoadMin,
                    b.PosMin, b.LoadMax,
                    color);

                formsPlot1.Plot.AddLine(
                    b.PosMax, b.LoadMin,
                    b.PosMax, b.LoadMax,
                    color);

                formsPlot1.Plot.AddLine(
                    b.PosMin, b.LoadMin,
                    b.PosMax, b.LoadMin,
                    color);

                formsPlot1.Plot.AddLine(
                    b.PosMin, b.LoadMax,
                    b.PosMax, b.LoadMax,
                    color);
            }
        }
    }
}
