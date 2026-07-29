// ========================================================
// 공통 변수 및 프로그램 초기화
//
// 주요 기능
// 1. PLC 주소 정의
// 2. 공통 변수 선언
// 3. 모델 데이터 저장
// 4. 그래프 데이터 저장
// 5. 프로그램 시작 초기화
// 6. 프로그램 종료 처리
// ========================================================
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace 인하테크개조
{
    public partial class Form1 : Form
    {
        // 모델별 생산량
        // =========================================================
        private sealed class QtyData
        {
            public int TotalQty { get; set; }
            public int PassQty { get; set; }
            public int NgQty { get; set; }
        }

        private readonly Dictionary<int, QtyData> modelQty =
            new Dictionary<int, QtyData>();


        // =========================================================
        // PLC 주소: 주소 확정 후 따옴표 안에 입력
        // =========================================================

        // 현재 모델번호
        private const string ADDR_MODEL_NO = "";

        // PLC 총 생산량
        private const string ADDR_TOTAL_QTY = "D110";

        // PLC 양품 생산량 ( 더블워드)
        private const string ADDR_PASS_QTY = "D120";

        // PLC 불량 발생량 ( 더블워드 )
        private const string ADDR_NG_QTY = "D124";

        // 고속 하강위치
        private const string ADDR_HIGH_DISTANCE = "D1000";

        // 압입 종료위치
        private const string ADDR_LOW_DISTANCE = "D1002";

        // 고속 속도
        private const string ADDR_HIGH_SPEED = "D1004";

        // 압입 속도
        private const string ADDR_LOW_SPEED = "D1006";

        // 설정 하중
        private const string ADDR_LOAD_SET = "D1008";

        // 대기 위치
        private const string ADDR_WAIT_POS = "D1100";

        // 실시간 하중
        private const string ADDR_LOAD_REAL = "D2002";

        // 실시간 거리
        private const string ADDR_POS_REAL = "D2000";

        // 자동/수동 상태
        private const string ADDR_AUTO_MANUAL = "P000";

        // 사이클 신호
        // 1 = 사이클 진행
        // 0 = 사이클 종료
        private const string ADDR_CYCLE_START = "D2006";

        // 그래프 수집 신호
        // 1 = 그래프 데이터 수집
        // 0 = 그래프 데이터 수집 정지
        private const string ADDR_GRAPH_START = "D2008";

        // PC 판정 OK 신호
        private const string ADDR_PC_OK = "D1010";

        // PC 판정 NG 신호
        private const string ADDR_PC_NG = "D1012";

        // 비상정지 신호
        private const string ADDR_EMG = "P004";

        // 에어리어 센서
        private const string ADDR_AREA_SENSOR = "P14"; // 원래는 P00E임 16진수로 변환한거임

        // 자동운전 중 신호
        private const string ADDR_AUTO_RUN = "P020";



        // 위치값 변환 배율
        private const double POS_SCALE = 100.0;

        // 하중값 변환 배율
        private const double LOAD_SCALE = 1.0;

        private readonly string saveFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "CycleData");

        private readonly string settingFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "CycleData",
            "model_settings.ini");


        private readonly string qtySettingFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "CycleData",
            "qty_settings.ini");        

        private sealed class BoxSpec
        {
            public bool Use { get; set; }
            public double PosMin { get; set; }
            public double PosMax { get; set; }
            public double LoadMin { get; set; }
            public double LoadMax { get; set; }

            public BoxSpec(bool use, double posMin, double posMax, double loadMin, double loadMax)
            {
                Use = use;
                PosMin = posMin;
                PosMax = posMax;
                LoadMin = loadMin;
                LoadMax = loadMax;
            }
        }

        private sealed class ModelConfig
        {
            public int ModelNo { get; set; }
            public string ModelName { get; set; }
            public double HighDistance { get; set; }
            public double LowDistance { get; set; }
            public double WaitPos { get; set; }
            public double HighSpeed { get; set; }
            public double LowSpeed { get; set; }
            public double LoadSet { get; set; }

            // 모델별 그래프 축 범위
            public double GraphXMin { get; set; }
            public double GraphXMax { get; set; }
            public double GraphYMin { get; set; }
            public double GraphYMax { get; set; }

            public BoxSpec[] Boxes { get; set; }
        }

        private readonly Dictionary<int, ModelConfig> modelConfigs =
            new Dictionary<int, ModelConfig>();

        private readonly List<double> servoX = new List<double>();
        private readonly List<double> loadY = new List<double>();
        // 그래프 축 범위
        private double graphXMin = -200;
        private double graphXMax = 200;
        private double graphYMin = 0;
        private double graphYMax = 1000;

        private readonly Timer plcTimer = new Timer();

        private int currentModelNo = 1;
        private bool plcConnected;

        // 현재 그래프 데이터 수집 중인지
        private bool collecting;

        // 이전 PLC 신호 상태
        private bool prevCycleStart;
        private bool prevGraphStart;

        // 현재 사이클 진행 상태
        private bool cycleRunning;

        // 현재 사이클 판정 완료 여부
        private bool cycleJudgeDone;

        // 마지막 판정 결과
        private bool lastJudgeOk;

        private bool prevEmg = false;

        private bool prevAreaSensor = false;

        private bool emgPopupShown = false;

        private bool plcDisconnectPopupShown = false;

        private bool emergencyCancelledCycle = false;

        // PLC에서 마지막으로 읽은 생산수량
        private ushort currentTotalQty;
        private int currentPassQty;
        private int currentNgQty;

        private ModelConfig CurrentConfig
        {
            get
            {
                if (modelConfigs.ContainsKey(currentModelNo))
                    return modelConfigs[currentModelNo];

                return modelConfigs.Values.OrderBy(x => x.ModelNo).First();
            }
        }

        private QtyData CurrentQty
        {
            get
            {
                if (!modelQty.ContainsKey(currentModelNo))
                {
                    modelQty[currentModelNo] = new QtyData();
                }

                return modelQty[currentModelNo];
            }
        }

        private void UpdateQtyLabel()
        {
            
            lblPqty.Text = CurrentQty.PassQty.ToString();
            lblNqty.Text = CurrentQty.NgQty.ToString();
        }

        private void InitializeProgramLogic()
        {
            btnSave.Click += btnSave_Click;
            btnCheckBoxSet.Click += btnCheckBoxSet_Click;
            btnModelEdit.Click += btnModelEdit_Click;
            btnAxisSet.Click += btnAxisSet_Click;
            btnOpenFile.Click += btnOpenFile_Click;
            txtModelSelcet.SelectedIndexChanged += txtModelSelcet_SelectedIndexChanged;

            FormClosing += Form1_FormClosing;
            Load += Form1_ProgramLoad;

            // PLC 통신 주기
            plcTimer.Interval = 50;
            plcTimer.Tick += PlcTimer_Tick;
        }

        private void Form1_ProgramLoad(object sender, EventArgs e)
        {
            Directory.CreateDirectory(saveFolderPath);

            InitDefaultModels();
            LoadModelSettings();                       
            RefreshModelCombo();

            if (txtModelSelcet.Items.Count > 0)
                txtModelSelcet.SelectedIndex = 0;
            

            prevCycleStart = false;
            prevGraphStart = false;
            cycleRunning = false;
            collecting = false;
            cycleJudgeDone = false;
            lastJudgeOk = false;

            ResetJudgeLamp();
            SetCommunicationLamp(false);
            SetAutoManualLamp(false);
            InitPlot();
           

            // XGT 통신 완성 후:
            try
            {
                plc.Connect("192.168.1.2");
                plcConnected = plc.IsConnected;

                SetCommunicationLamp(plcConnected);

                if (plcConnected)
                {
                    WriteCurrentModelSettingsToPlc();
                    ResetJudgeResultToPlc();

                    // 현재 설비 기준
                    // false = 정상
                    // true  = 비상정지 눌림
                    bool emgPressed = plc.ReadBit(ADDR_EMG);

                    // 현재 비상정지 신호 상태 저장
                    prevEmg = emgPressed;

                    // 프로그램 실행 시 이미 비상정지가 눌려 있으면 1회 표시
                    if (emgPressed)
                    {
                        // 타이머 시작 후 같은 비상정지로 팝업이 또 뜨지 않도록
                        emgPopupShown = true;

                        EmergencyReset();

                        MessageBox.Show(
                            "비상정지가 눌려 있습니다.\r\n비상정지를 해제한 후 운전하십시오.",
                            "비상정지",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    else
                    {
                        emgPopupShown = false;
                    }

                    plcTimer.Start();
                }
            }
            catch (Exception ex)
            {
                plcConnected = false;
                SetCommunicationLamp(false);

                MessageBox.Show(
                    "PLC 연결 실패\r\n" + ex.Message,
                    "통신 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }       

        private void SaveQtySettings()
        {
            try
            {
                Directory.CreateDirectory(saveFolderPath);

                List<string> lines = new List<string>();

                foreach (var item in modelQty.OrderBy(x => x.Key))
                {
                    lines.Add(
                        item.Key + "," +
                        item.Value.TotalQty + "," +
                        item.Value.PassQty + "," +
                        item.Value.NgQty);
                }

                File.WriteAllLines(qtySettingFilePath, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "생산량 저장 실패\r\n" + ex.Message,
                    "저장 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void LoadQtySettings()
        {
            modelQty.Clear();

            if (!File.Exists(qtySettingFilePath))
                return;

            try
            {
                string[] lines = File.ReadAllLines(qtySettingFilePath);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] p = line.Split(',');

                    if (p.Length < 4)
                        continue;

                    int modelNo;
                    int total;
                    int pass;
                    int ng;

                    if (!int.TryParse(p[0], out modelNo))
                        continue;

                    if (!int.TryParse(p[1], out total))
                        total = 0;

                    if (!int.TryParse(p[2], out pass))
                        pass = 0;

                    if (!int.TryParse(p[3], out ng))
                        ng = 0;

                    modelQty[modelNo] = new QtyData
                    {
                        TotalQty = total,
                        PassQty = pass,
                        NgQty = ng
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "생산량 불러오기 실패\r\n" + ex.Message,
                    "불러오기 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            plcTimer.Stop();

            SaveModelSettings();                       

            if (plc != null)
                plc.Disconnect();
        }
    }
}
