// ========================================================
// PLC 통신 관리
// PLC와 주기적으로 통신하면서
// 실시간 값, 자동/수동, 그래프, 판정 등을 처리
// ========================================================

using System;
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
                // 위치값 32비트 일괄 읽기
                // 예: ADDR_POS_REAL = "D3600"
                // D3600~D3601을 ReadDWord 내부에서 한 번에 읽어야 함
                // ========================================================
                int rawPos = plc.ReadDWord(ADDR_POS_REAL);

                // 실시간 하중값 16비트 읽기
                short rawLoad =
                    unchecked((short)plc.ReadWord(ADDR_LOAD_REAL));

                // PLC 데이터 스케일 변환
                double pos = rawPos / POS_SCALE;
                double load = rawLoad / LOAD_SCALE;

                // 자동/수동 상태
                bool autoMode = plc.ReadBit(ADDR_AUTO_MANUAL);

                // 사이클 시작
                bool cycleStart = plc.ReadBit(ADDR_CYCLE_START);

                // 그래프 수집 시작
                bool graphStart = plc.ReadBit(ADDR_GRAPH_START);

                // 사이클 종료
                bool cycleEnd = plc.ReadBit(ADDR_CYCLE_END);

                // 읽은 데이터 화면 및 그래프에 반영
                UpdateRealtimePlcValues(
                    pos,
                    load,
                    autoMode,
                    cycleStart,
                    graphStart,
                    cycleEnd);
            }
            catch
            {
                plcConnected = false;
               
            }
        }

        private void UpdateRealtimePlcValues(
                    double pos, double load, bool autoMode,
                    bool cycleStart, bool graphStart, bool cycleEnd)
                {
                    //통신 정상
                    plcConnected = true;
                    //통신 ON 램프
                    SetCommunicationLamp(true);
                    // 자동/수동 램프
                    SetAutoManualLamp(autoMode);
                    
                    //실시간 거리 표시
                    txtPosReal.Text = pos.ToString("0.000");
                    //실시간 하중 표시
                    txtLoadReal.Text = load.ToString("0.0");

                // =====================================
                // 사이클이 새로 시작되면
                // 이전 데이터 모두 삭제
                // =====================================
            if (cycleStart && !prevCycleStart)
                    {
                        servoX.Clear();
                        loadY.Clear();
                        collecting = true;
                        cycleJudgeDone = false;
                        lastJudgeOk = false;
                        ResetJudgeLamp();
                        DrawPlot();
                    }
        
                    prevCycleStart = cycleStart;
        
                    if (collecting && graphStart)
                    {
                        if (servoX.Count > 0)
                        {
                            double previous = servoX[servoX.Count - 1];
                            if (pos < previous - 0.02)
                                pos = previous;
                        }
        
                        servoX.Add(pos);
                        loadY.Add(load);
                        DrawPlot();
                    }
        
                    if (cycleEnd)
                        FinishCycleJudge();
                }


            // ========================================================
            // 현재 모델의 설정값을 PLC로 전송
            //
            // 저장 버튼을 누르면 호출
            //
            // PLC 주소가 정해지면
            // 아래 WriteWord / WriteDWord 주석을 해제하면 된다.
            // ========================================================
        private void WriteCurrentModelSettingsToPlc()
                {
                    if (!plcConnected) return;
        
                    ModelConfig c = CurrentConfig;
        
                    // 주소 및 PLC 배율 확정 후 사용

                    //고속 이송거리
                    // plc.WriteDWord(ADDR_HIGH_DISTANCE, (int)Math.Round(c.HighDistance * 10000));

                    //압입 이송거리
                    // plc.WriteDWord(ADDR_LOW_DISTANCE,  (int)Math.Round(c.LowDistance * 10000));

                    //고속 속도
                    // plc.WriteWord(ADDR_HIGH_SPEED,     (int)Math.Round(c.HighSpeed));

                    // 압입 속도
                    // plc.WriteWord(ADDR_LOW_SPEED,      (int)Math.Round(c.LowSpeed));

                    // 압입 하중
                    // plc.WriteWord(ADDR_LOAD_SET,       (int)Math.Round(c.LoadSet * 10));
                }
    }
}
