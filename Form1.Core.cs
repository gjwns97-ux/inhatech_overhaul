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
        // =========================================================
        // PLC 주소: 주소 확정 후 따옴표 안에 입력
        // =========================================================

        // 현재 모델번호
        private const string ADDR_MODEL_NO = "";

        // 고속 이송거리
        private const string ADDR_HIGH_DISTANCE = "";

        // 압입 이송거리
        private const string ADDR_LOW_DISTANCE = "";

        // 고속 속도
        private const string ADDR_HIGH_SPEED = "";

        // 압입 속도
        private const string ADDR_LOW_SPEED = "";

        // 설정 하중
        private const string ADDR_LOAD_SET = "";

        // 실시간 하중
        private const string ADDR_LOAD_REAL = "";

        // 실시간 거리
        private const string ADDR_POS_REAL = "";

        // 자동/수동 상태
        private const string ADDR_AUTO_MANUAL = "";

        // 사이클 시작 신호
        private const string ADDR_CYCLE_START = "";

        // 그래프 그리기 시작 신호
        private const string ADDR_GRAPH_START = "";

        // 사이클 종료 신호
        private const string ADDR_CYCLE_END = "";

        // PC 판정 결과 전송 신호
        private const string ADDR_PC_RESULT = "";

        // 위치값 변환 배율
        private const double POS_SCALE = 10000.0;

        // 하중값 변환 배율
        private const double LOAD_SCALE = 10.0;

        private readonly string saveFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "CycleData");

        private readonly string settingFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "CycleData",
            "model_settings.ini");

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
            public double HighSpeed { get; set; }
            public double LowSpeed { get; set; }
            public double LoadSet { get; set; }
            public BoxSpec[] Boxes { get; set; }
        }

        private readonly Dictionary<int, ModelConfig> modelConfigs =
            new Dictionary<int, ModelConfig>();

        private readonly List<double> servoX = new List<double>();
        private readonly List<double> loadY = new List<double>();
        private readonly Timer plcTimer = new Timer();

        private int currentModelNo = 1;
        private bool plcConnected;
        private bool collecting;
        private bool prevCycleStart;
        private bool cycleJudgeDone;
        private bool lastJudgeOk;

        private ModelConfig CurrentConfig
        {
            get
            {
                if (modelConfigs.ContainsKey(currentModelNo))
                    return modelConfigs[currentModelNo];

                return modelConfigs.Values.OrderBy(x => x.ModelNo).First();
            }
        }

        private void InitializeProgramLogic()
        {
            btnSave.Click += btnSave_Click;
            btnCheckBoxSet.Click += btnCheckBoxSet_Click;
            btnModelEdit.Click += btnModelEdit_Click;
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

            ResetJudgeLamp();
            SetCommunicationLamp(false);
            SetAutoManualLamp(false);
            InitPlot();

            // XGT 통신 완성 후:
            // plcConnected = plc.Connect(...);
            // if (plcConnected)
            //     plcTimer.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            plcTimer.Stop();
            SaveModelSettings();

            // plc.Disconnect();
        }
    }
}
