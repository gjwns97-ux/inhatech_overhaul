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

        private void ShowConfiguredPositionValues()
        {
            // 압입부 설정 거리
            if (CurrentConfig.Boxes[0].Use)
            {
                txtPtnMin1.Text = CurrentConfig.Boxes[0].PosMin.ToString("0.###");
                txtPtnMax1.Text = CurrentConfig.Boxes[0].PosMax.ToString("0.###");
            }
            else
            {
                txtPtnMin1.Text = "";
                txtPtnMax1.Text = "";
            }

            // 밀착부 설정 거리
            if (CurrentConfig.Boxes[1].Use)
            {
                txtPtnMin2.Text = CurrentConfig.Boxes[1].PosMin.ToString("0.###");
                txtPtnMax2.Text = CurrentConfig.Boxes[1].PosMax.ToString("0.###");
            }
            else
            {
                txtPtnMin2.Text = "";
                txtPtnMax2.Text = "";
            }
        }

        private void ShowMeasuredValues()
        {

            ShowConfiguredPositionValues();

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

        private void ClearCycleResultValues()
        {
            // 실제 측정 하중값만 초기화
            txtLoadMin1.Text = "";
            txtLoadMax1.Text = "";

            txtLoadMin2.Text = "";
            txtLoadMax2.Text = "";

            // 설정 거리값은 계속 표시
            ShowConfiguredPositionValues();
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
            // 해당 구간을 사용하지 않는 경우
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
                txtLoadMin.Text = "";
                txtLoadMax.Text = "";
                return;
            }

            // PosMin, PosMax를 반대로 입력해도 정상 동작
            double rangeMin = Math.Min(box.PosMin, box.PosMax);
            double rangeMax = Math.Max(box.PosMin, box.PosMax);

            bool found = false;

            double minLoad = double.MaxValue;
            double maxLoad = double.MinValue;

            for (int i = 0; i < count; i++)
            {
                double position = servoX[i];
                double load = loadY[i];

                // 압입구간 설정의 거리 Min ~ Max 범위 안에 있는 데이터만 사용
                if (position < rangeMin || position > rangeMax)
                    continue;

                found = true;

                // 구간 내 최소 하중
                if (load < minLoad)
                    minLoad = load;

                // 구간 내 최대 하중
                if (load > maxLoad)
                    maxLoad = load;
            }

            // 설정 구간 안에 측정 데이터가 하나도 없는 경우
            if (!found)
            {
                txtLoadMin.Text = "";
                txtLoadMax.Text = "";
                return;
            }

            // 설정 거리 구간 안에서 측정된 최소 / 최대 하중 표시
            txtLoadMin.Text = minLoad.ToString("0.###");
            txtLoadMax.Text = maxLoad.ToString("0.###");
        }

        private void FinishCycleJudge()
        {
            if (cycleJudgeDone)
                return;

            collecting = false;

            // 최종 판정
            lastJudgeOk = CheckFinalPass();

            if (lastJudgeOk)
                CurrentQty.PassQty++;
            else
                CurrentQty.NgQty++;

            UpdateQtyLabel();
            SaveQtySettings();
                        
            cycleJudgeDone = true;

            // 화면 OK/NG 램프 표시
            SetJudgeLamp(lastJudgeOk);

            // 실제 측정값 표시
            ShowMeasuredValues();

            // PLC에 OK/NG 결과 전송
            WriteJudgeResultToPlc(lastJudgeOk);

            // 결과 저장
            SaveCycleData();

            // 최종 그래프 표시
            DrawPlot();
        }

        private void EmergencyReset()
        {
            // 현재 사이클 정지
            collecting = false;
            cycleRunning = false;

            // 비상정지된 사이클은 판정 및 저장 금지
            cycleJudgeDone = true;
            emergencyCancelledCycle = true;
            lastJudgeOk = false;

            // 그래프 데이터 삭제
            servoX.Clear();
            loadY.Clear();

            // 판정 램프 초기화
            ResetJudgeLamp();

            // PC → PLC OK/NG 초기화
            ResetJudgeResultToPlc();

            // 압입부/밀착부 측정 결과 초기화
            ClearCycleResultValues();

            // 빈 그래프로 다시 표시
            DrawPlot();
        }

        private void AreaSensorReset()
        {
            collecting = false;
            cycleRunning = false;

            cycleJudgeDone = true;
            emergencyCancelledCycle = true;
            lastJudgeOk = false;

            servoX.Clear();
            loadY.Clear();

            ResetJudgeLamp();
            ResetJudgeResultToPlc();

            ClearCycleResultValues();

            // 서보 위치 리얼값만 초기화
            // 서보 하중 리얼값과 생산량은 유지
            txtPosReal.Text = "0";

            DrawPlot();
        }

        private void ClearMeasuredValues()
        {
            txtLoadMin1.Text = "";
            txtLoadMax1.Text = "";

            txtLoadMin2.Text = "";
            txtLoadMax2.Text = "";

            ShowConfiguredPositionValues();
        }

        private void ResetCycleState()
        {
            // 이전 사이클 측정값 초기화
            
            txtLoadMin1.Text = "";
            txtLoadMax1.Text = "";

           
            txtLoadMin2.Text = "";
            txtLoadMax2.Text = "";

            servoX.Clear();
            loadY.Clear();

            collecting = false;
            cycleRunning = false;
            cycleJudgeDone = false;
            lastJudgeOk = false;

            prevCycleStart = false;
            prevGraphStart = false;

            txtLoadReal.Text = "0";
            txtPosReal.Text = "0";

            ResetJudgeLamp();

            // PLC OK/NG 판정 신호 초기화
            ResetJudgeResultToPlc();
            ShowConfiguredPositionValues();
            DrawPlot();
        }     
        
    }
}
