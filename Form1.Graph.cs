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

                    // 그래프 표시 범위 설정 X축 0~200mm, Y축 0~1000kgf
                    formsPlot1.Plot.SetAxisLimits(0, 200, 0, 1000);

                    // 격자선 표시
                    formsPlot1.Plot.Grid(true);

                    formsPlot1.Refresh();
                }

                private void DrawPlot()
                {
                    formsPlot1.Plot.Clear();
                    AddBoxesToPlot(cycleJudgeDone);
        
                    if (servoX.Count > 1)
                    {
                        formsPlot1.Plot.AddScatterLines(
                            servoX.ToArray(), loadY.ToArray(),
                            Color.Blue, 2);
                    }
        
                    formsPlot1.Plot.XLabel("거리 [mm]");
                    formsPlot1.Plot.YLabel("하중 [kgf]");
                    formsPlot1.Plot.SetAxisLimits(0, 200, 0, 1000);
                    formsPlot1.Plot.Grid(true);
                    formsPlot1.Refresh();
                }

                private void AddBoxesToPlot(bool showResultColor)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        BoxSpec b = CurrentConfig.Boxes[i];
                        if (!b.Use) continue;
        
                        Color color = Color.Green;
        
                        if (showResultColor &&
                            !CheckBoxPass(servoX, loadY, b, i))
                            color = Color.Red;
        
                        formsPlot1.Plot.AddLine(b.PosMin,b.LoadMin,b.PosMin,b.LoadMax,color);
                        formsPlot1.Plot.AddLine(b.PosMax,b.LoadMin,b.PosMax,b.LoadMax,color);
                        formsPlot1.Plot.AddLine(b.PosMin,b.LoadMin,b.PosMax,b.LoadMin,color);
                        formsPlot1.Plot.AddLine(b.PosMin,b.LoadMax,b.PosMax,b.LoadMax,color);
                    }
                }
    }
}
