// ========================================================
// OK / NG 판정 관리
//
// 주요 기능
// 1. 압입부 판정
// 2. 밀착부 판정
// 3. 전체 최종판정
// 4. 사이클 종료 처리
// 5. 사이클 초기화
// 6. 테스트용 그래프 데이터 추가
// ========================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace 인하테크개조
{
    public partial class Form1 : Form
    {
        private bool CheckBoxPass(
List<double> posList,
List<double> loadList,
BoxSpec box,
int boxIndex)
        {
            if (!box.Use)
                return true;

            if (posList.Count == 0 ||
                loadList.Count == 0 ||
                posList.Count != loadList.Count)
                return false;

            // 해당 거리 구간에 실제 측정 데이터가 있는지 확인
            bool hasDataInRange = false;
            double maxLoad = double.MinValue;

            for (int i = 0; i < posList.Count; i++)
            {
                if (posList[i] >= box.PosMin &&
                    posList[i] <= box.PosMax)
                {
                    hasDataInRange = true;

                    if (loadList[i] > maxLoad)
                        maxLoad = loadList[i];
                }
            }

            if (!hasDataInRange)
                return false;

            // 밀착부
            // 설정 거리 구간 안의 최대 하중으로 판정
            if (boxIndex == 1)
            {
                return maxLoad >= box.LoadMin &&
                       maxLoad <= box.LoadMax;
            }

            // 압입부
            // PosMin 지점 하중과 PosMax 지점 하중으로 판정
            double loadAtMin = GetLoadAtPosition(
                posList,
                loadList,
                box.PosMin);

            double loadAtMax = GetLoadAtPosition(
                posList,
                loadList,
                box.PosMax);

            return loadAtMin >= box.LoadMin &&
                   loadAtMin <= box.LoadMax &&
                   loadAtMax >= box.LoadMin &&
                   loadAtMax <= box.LoadMax;
        }

        private bool CheckFinalPass()
                {
                    bool anyUse = false;
        
                    for (int i = 0; i < 2; i++)
                    {
                        if (!CurrentConfig.Boxes[i].Use) continue;
                        anyUse = true;
        
                        if (!CheckBoxPass(servoX, loadY, CurrentConfig.Boxes[i], i))
                            return false;
                    }
        
                    return anyUse;
                }

        private void ShowMeasuredValues()
        {
            // 압입부
            ShowBoxMeasuredValue(
                CurrentConfig.Boxes[0],
                0,
                txtPtnMin1,
                txtPtnMax1,
                txtLoadMin1,
                txtLoadMax1);

            // 밀착부
            ShowBoxMeasuredValue(
                CurrentConfig.Boxes[1],
                1,
                txtPtnMin2,
                txtPtnMax2,
                txtLoadMin2,
                txtLoadMax2);
        }

        // ========================================================
        // 지정한 거리에서의 하중 계산
        //
        // 예:
        // 측정점 29.8mm = 100kgf
        // 측정점 30.2mm = 120kgf
        //
        // 목표 거리가 30.0mm라면 보간 계산하여
        // 약 110kgf를 반환
        // ========================================================
        private double GetLoadAtPosition(
            List<double> posList,
            List<double> loadList,
            double targetPos)
        {
            int count = Math.Min(posList.Count, loadList.Count);

            if (count == 0)
                return 0;

            // 앞뒤 두 측정점 사이에 목표 거리가 있는지 확인
            for (int i = 1; i < count; i++)
            {
                double x1 = posList[i - 1];
                double y1 = loadList[i - 1];

                double x2 = posList[i];
                double y2 = loadList[i];

                // 목표 거리가 두 측정점 사이에 있는 경우
                if (targetPos >= x1 && targetPos <= x2)
                {
                    // 동일한 거리값이 연속으로 들어온 경우
                    if (x2 == x1)
                        return y2;

                    // 선형 보간 계산
                    double ratio = (targetPos - x1) / (x2 - x1);

                    return y1 + ((y2 - y1) * ratio);
                }
            }

            // 목표 거리가 측정 범위 밖이면
            // 가장 가까운 거리의 하중 사용
            int nearestIndex = 0;
            double nearestDiff = Math.Abs(posList[0] - targetPos);

            for (int i = 1; i < count; i++)
            {
                double diff = Math.Abs(posList[i] - targetPos);

                if (diff < nearestDiff)
                {
                    nearestDiff = diff;
                    nearestIndex = i;
                }
            }

            return loadList[nearestIndex];
        }


        // ========================================================
        // 압입부/밀착부의 설정 거리 Min/Max 지점에서
        // 실제 하중값을 계산하여 화면에 표시
        // ========================================================
        private void ShowBoxMeasuredValue(
    BoxSpec box,
    int boxIndex,
    TextBox txtPosMin,
    TextBox txtPosMax,
    TextBox txtLoadMin,
    TextBox txtLoadMax)
        {
            if (!box.Use)
            {
                txtPosMin.Text = "";
                txtPosMax.Text = "";
                txtLoadMin.Text = "";
                txtLoadMax.Text = "";
                return;
            }

            int count = Math.Min(servoX.Count, loadY.Count);

            if (count == 0)
            {
                txtPosMin.Text = "";
                txtPosMax.Text = "";
                txtLoadMin.Text = "";
                txtLoadMax.Text = "";
                return;
            }

            int minIndex = -1;
            int maxIndex = -1;

            // 설정 위치를 처음 통과한 실제 측정점 찾기
            for (int i = 0; i < count; i++)
            {
                if (minIndex == -1 && servoX[i] >= box.PosMin)
                    minIndex = i;

                if (maxIndex == -1 && servoX[i] >= box.PosMax)
                    maxIndex = i;

                if (minIndex != -1 && maxIndex != -1)
                    break;
            }

            // PosMin에 도달하지 못한 경우
            if (minIndex == -1)
            {
                txtPosMin.Text = "";
                txtLoadMin.Text = "";
            }
            else
            {
                // 실제로 수집된 위치값
                txtPosMin.Text =
                    servoX[minIndex].ToString("0.###");

                // 설정된 PosMin 위치에서 보간한 하중값
                txtLoadMin.Text =
                    GetLoadAtPosition(
                        servoX,
                        loadY,
                        box.PosMin)
                    .ToString("0.###");
            }

            // PosMax에 도달하지 못한 경우
            if (maxIndex == -1)
            {
                txtPosMax.Text = "";
                txtLoadMax.Text = "";
            }
            else
            {
                // 실제로 수집된 위치값
                txtPosMax.Text =
                    servoX[maxIndex].ToString("0.###");

                // 설정된 PosMax 위치에서 보간한 하중값
                txtLoadMax.Text =
                    GetLoadAtPosition(
                        servoX,
                        loadY,
                        box.PosMax)
                    .ToString("0.###");
            }
        }

        private void FinishCycleJudge()
                {
                    if (cycleJudgeDone) return;
        
                    collecting = false;
                    cycleJudgeDone = true;
                    lastJudgeOk = CheckFinalPass();
                    SetJudgeLamp(lastJudgeOk);
                     // 실제 측정값 표시
                     ShowMeasuredValues();

            // XGT 통신 완성 후:
            // plc.WriteWord(ADDR_PC_RESULT, lastJudgeOk ? 0 : 1);

            SaveCycleData();
                    DrawPlot();
                }

        private void ResetCycleState()
        {
            // 이전 사이클 측정값 초기화
            txtPtnMin1.Text = "";
            txtPtnMax1.Text = "";
            txtLoadMin1.Text = "";
            txtLoadMax1.Text = "";

            txtPtnMin2.Text = "";
            txtPtnMax2.Text = "";
            txtLoadMin2.Text = "";
            txtLoadMax2.Text = "";

            servoX.Clear();
            loadY.Clear();

            collecting = false;
            cycleJudgeDone = false;
            lastJudgeOk = false;
            prevCycleStart = false;

            txtLoadReal.Text = "0.0";
            txtPosReal.Text = "0.0";

            ResetJudgeLamp();
            DrawPlot();
        }

        // ========================================================
        // 테스트용 그래프 데이터 추가
        //
        // PLC 연결 전 그래프와 판정 기능을 시험할 때 사용
        //
        // position : 테스트 거리
        // load     : 테스트 하중
        // ========================================================
        private void AddTestPoint(double position, double load)
                {
                    servoX.Add(position);
                    loadY.Add(load);
                    txtPosReal.Text = position.ToString("0.000");
                    txtLoadReal.Text = load.ToString("0.0");
                    DrawPlot();
                }
    }
}
