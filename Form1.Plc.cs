// ========================================================
// PLC 통신 관리
// PLC와 주기적으로 통신하면서
// 실시간 값, 자동/수동, 그래프, 판정 등을 처리
// ========================================================

using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace 인하테크개조
{
    public partial class Form1 : Form
    {
        private void PlcTimer_Tick(object sender, EventArgs e)
        {


            try
            { 
                // ========================================================
                // 실시간 위치값 읽기
                // ADDR_POS_REAL이 D워드 2개를 사용하는 32비트 값일 때 사용
                // 예: D3600, D3601
                // ========================================================
                int rawPos = plc.ReadDWord(ADDR_POS_REAL);

                // 실시간 하중값 16비트 읽기
                short rawLoad = plc.ReadInt16(ADDR_LOAD_REAL);

                ushort plcTotalQty = plc.ReadWord(ADDR_TOTAL_QTY);

                // PLC 데이터 스케일 변환
                double pos = rawPos / POS_SCALE;
                double load = rawLoad / LOAD_SCALE;

                // PLC 상태 신호 읽기
                bool autoMode = plc.ReadBit(ADDR_AUTO_MANUAL);

                // 1 = 사이클 진행, 0 = 사이클 종료
                bool cycleStart = plc.ReadWord(ADDR_CYCLE_START) != 0;

                // 1 = 그래프 수집, 0 = 그래프 수집 정지
                bool graphStart = plc.ReadWord(ADDR_GRAPH_START) != 0;

                // false = 정상, true = 비상정지 눌림
                bool emgPressed = plc.ReadBit(ADDR_EMG);
                bool emgActive = emgPressed;

                bool areaSensorActive = plc.ReadBit(ADDR_AREA_SENSOR);

                if (areaSensorActive && !prevAreaSensor)
                {
                    AreaSensorReset();
                }

                prevAreaSensor = areaSensorActive;

                // 비상정지가 눌린 상태
                if (emgPressed)
                {
                    // 아직 팝업을 표시하지 않았을 때만 1회 실행
                    if (!emgPopupShown)
                    {
                        // MessageBox보다 먼저 true로 변경해야 중복 방지됨
                        emgPopupShown = true;

                        EmergencyReset();

                        MessageBox.Show(
                            "비상정지가 눌렸습니다.",
                            "비상정지",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    emgPopupShown = false;

                    // 비상정지가 방금 해제된 순간
                    if (prevEmg)
                    {
                        // 현재 PLC 신호가 이미 ON이어도
                        // 사이클/그래프 시작 상승엣지를 다시 인식시킴
                        prevCycleStart = false;
                        prevGraphStart = false;

                        cycleRunning = false;
                        collecting = false;
                    }
                }

                prevEmg = emgPressed;


                UpdateRealtimePlcValues(
                pos,
                load,
                autoMode,
                cycleStart,
                graphStart,
                plcTotalQty,
                emgActive,
                areaSensorActive);

            }
            catch (Exception)
            {
                plcConnected = false;
                SetCommunicationLamp(false);

                if (!plcDisconnectPopupShown)
                {
                    plcDisconnectPopupShown = true;

                    MessageBox.Show(
                        "PLC와 연결이 끊어졌습니다.\r\n랜선 또는 PLC 전원을 확인하세요.",
                        "PLC 통신 오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        private void UpdateRealtimePlcValues(
            double pos,
            double load,
            bool autoMode,
            bool cycleStart,
            bool graphStart,
            ushort plcTotalQty,
            bool emgActive,
            bool areaSensorActive)
        {
            // 통신 정상
            plcConnected = true;

            // 통신 및 자동/수동 램프
            SetCommunicationLamp(true);
            plcDisconnectPopupShown = false;
            SetAutoManualLamp(autoMode);

            // 실시간 값 표시
            txtPosReal.Text = pos == 0 ? "0" : pos.ToString("0.00");
            txtLoadReal.Text = load == 0 ? "0" : load.ToString("0");

            // PLC D110 값을 총생산량으로 표시

            lblQty.Text = plcTotalQty.ToString();

            if (areaSensorActive)
            {
                collecting = false;
                cycleRunning = false;

                prevCycleStart = cycleStart;
                prevGraphStart = graphStart;

                return;
            }

            // 비상정지 중에는 실시간값과 생산량만 표시하고
            // 사이클 및 그래프 처리는 하지 않음
            if (emgActive)
            {
                collecting = false;

                return;
            }

            // ========================================================
            // 사이클 시작 상승엣지
            // ADDR_CYCLE_START: 0 → 1
            //
            // 새 사이클이 시작되면 이전 측정 결과를 초기화
            // 모델 설정값은 초기화하지 않음
            // ========================================================
            if (cycleStart && !prevCycleStart)
            {
                cycleRunning = true;                

                cycleJudgeDone = false;
                emergencyCancelledCycle = false;
                lastJudgeOk = false;
                collecting = false;

                servoX.Clear();
                loadY.Clear();

                ResetJudgeLamp();
                DrawPlot();
                ResetJudgeResultToPlc();
            }

            // ========================================================
            // 그래프 시작 상승엣지
            // ADDR_GRAPH_START: 0 → 1
            // ========================================================
            if (graphStart && !prevGraphStart)
            {
                collecting = true;

                // 새 사이클 시작 전에 그래프 신호가 들어온 경우에도
                // 이전 그래프 데이터가 섞이지 않도록 초기화
                if (!cycleRunning)
                {
                    servoX.Clear();
                    loadY.Clear();

                    cycleJudgeDone = false;
                    lastJudgeOk = false;

                    ResetJudgeLamp();
                    DrawPlot();
                }
            }

            // ========================================================
            // 그래프 신호가 살아 있는 동안만 데이터 수집
            // ========================================================
            if (collecting && graphStart)
            {
                double graphPos = pos;

                // 위치값이 조금 역행할 때 그래프가 뒤로 꺾이지 않도록 보정
                if (servoX.Count > 0)
                {
                    double previous = servoX[servoX.Count - 1];

                    if (graphPos < previous - 0.02)
                        graphPos = previous;
                }

                servoX.Add(graphPos);
                loadY.Add(load);               
                DrawPlot();
            }

            // ========================================================
            // 그래프 종료 하강엣지
            // ADDR_GRAPH_START: 1 → 0
            // ========================================================
            if (!graphStart && prevGraphStart)
            {
                collecting = false;
            }

            // ========================================================
            // 사이클 종료 하강엣지
            // ADDR_CYCLE_START: 1 → 0
            //
            // 별도의 ADDR_CYCLE_END는 사용하지 않음
            // ========================================================
            if (!cycleStart && prevCycleStart)
            {
                cycleRunning = false;
                collecting = false;

                if (!cycleJudgeDone && !emergencyCancelledCycle)
                {
                    FinishCycleJudge();
                }

                emergencyCancelledCycle = false;
            }

            // 이번 신호를 다음 주기의 이전 상태로 저장
            prevCycleStart = cycleStart;
            prevGraphStart = graphStart;
        }

        // ========================================================
        // 현재 모델의 설정값을 PLC로 전송
        //
        // 저장 버튼을 누르면 호출
        // ========================================================

        private void WriteJudgeResultToPlc(bool isOk)
        {
            try
            {
                plc.WriteWord(ADDR_PC_OK, isOk ? 1 : 0);
                plc.WriteWord(ADDR_PC_NG, isOk ? 0 : 1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "판정값 PLC 전송 실패\r\n" +
                    "OK 주소: " + ADDR_PC_OK + "\r\n" +
                    "NG 주소: " + ADDR_PC_NG + "\r\n\r\n" +
                    ex.Message,
                    "PLC 쓰기 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                throw;
            }
        }

        private void ResetJudgeResultToPlc()
        {
            // PLC 연결 전에는 쓰기하지 않음
            if (plc == null || !plc.IsConnected)
                return;

            plc.WriteWord(ADDR_PC_OK, 0);
            plc.WriteWord(ADDR_PC_NG, 0);
        }



        private void WriteCurrentModelSettingsToPlc()
        {
            if (!plcConnected)
                return;

            ModelConfig c = CurrentConfig;

            // 고속 하강 위치: 32비트
            // 거리 (PC값 × 100 → PLC)
            plc.WriteDWord(
                ADDR_HIGH_DISTANCE,
                (int)Math.Round(c.HighDistance * POS_SCALE));


            // 압입 종료 위치: 32비트
            plc.WriteDWord(
            ADDR_LOW_DISTANCE,
            (int)Math.Round(c.LowDistance * POS_SCALE));

            // 대기 위치 : 32비트
            plc.WriteDWord(ADDR_WAIT_POS,
            (int)Math.Round(CurrentConfig.WaitPos * POS_SCALE));

            // 고속 속도: 16비트
            plc.WriteWord(
                ADDR_HIGH_SPEED,
                checked((ushort)Math.Round(c.HighSpeed)));

            // 압입 속도: 16비트
            plc.WriteWord(
                ADDR_LOW_SPEED,
                checked((ushort)Math.Round(c.LowSpeed)));

            // 설정 하중: 16비트
            // 하중 (PC값 × 1 → PLC)
            plc.WriteWord(
            ADDR_LOAD_SET,
            checked((ushort)Math.Round(c.LoadSet * LOAD_SCALE)));
        }
    }
}