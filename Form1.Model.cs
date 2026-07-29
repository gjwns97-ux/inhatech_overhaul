// ========================================================
// 모델 설정 및 저장 관리
//
// 주요 기능
// 1. 기본 모델 5개 생성
// 2. 모델 선택 콤보박스 갱신
// 3. 선택한 모델의 설정값 화면 표시
// 4. 저장 버튼 처리
// 5. 설정값 파일 저장 및 불러오기
// ========================================================

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace 인하테크개조
{
    public partial class Form1 : Form
    {
        private const int DEFAULT_MODEL_COUNT = 5;

        private void InitDefaultModels()
        {
            modelConfigs.Clear();

            for (int i = 1; i <= DEFAULT_MODEL_COUNT; i++)
            {
                modelConfigs[i] = CreateDefaultModel(i);
            }
        }

        private ModelConfig CreateDefaultModel(int no)
        {
            return new ModelConfig
            {
                ModelNo = no,
                ModelName = "MODEL_" + no,
                HighDistance = 0,
                LowDistance = 0,
                WaitPos = 0,
                HighSpeed = 0,
                LowSpeed = 0,
                LoadSet = 0,
                GraphXMin = -200,
                GraphXMax = 200,
                GraphYMin = 0,
                GraphYMax = 1000,
                Boxes = new[]
                {
            new BoxSpec(true,0,0,0,0),
            new BoxSpec(true,0,0,0,0)
        }
            };
        }

        // ========================================================
        // 모델 선택 ComboBox 새로고침
        //
        // 모델 추가, 삭제, 이름 변경 후 호출
        // 기존에 선택 중이던 모델은 가능하면 유지한다.
        // ========================================================

        private void AddModel()
        {
            int nextNo = 1;

            if (modelConfigs.Count > 0)
                nextNo = modelConfigs.Keys.Max() + 1;

            modelConfigs[nextNo] = CreateDefaultModel(nextNo);

            RefreshModelCombo();

            currentModelNo = nextNo;
        }

        private void RefreshModelCombo()
        {
            int oldNo = currentModelNo;

            txtModelSelcet.BeginUpdate();
            txtModelSelcet.Items.Clear();

            foreach (ModelConfig cfg in modelConfigs.Values.OrderBy(x => x.ModelNo))
                txtModelSelcet.Items.Add(cfg);

            txtModelSelcet.DisplayMember = "ModelName";
            txtModelSelcet.ValueMember = "ModelNo";
            txtModelSelcet.EndUpdate();

            for (int i = 0; i < txtModelSelcet.Items.Count; i++)
            {
                ModelConfig cfg = txtModelSelcet.Items[i] as ModelConfig;

                if (cfg != null && cfg.ModelNo == oldNo)
                {
                    txtModelSelcet.SelectedIndex = i;
                    return;
                }
            }

            if (txtModelSelcet.Items.Count > 0)
                txtModelSelcet.SelectedIndex = 0;
        }

        // ========================================================
        // 모델 선택 ComboBox 변경 이벤트
        //
        // 사용자가 모델을 변경했을 때
        // 해당 모델 설정값을 화면에 표시한다.
        // ========================================================
        private void txtModelSelcet_SelectedIndexChanged(object sender, EventArgs e)
        {
            ModelConfig cfg = txtModelSelcet.SelectedItem as ModelConfig;

            if (cfg == null)
                return;

            currentModelNo = cfg.ModelNo;
            lblCurrentModel.Text = cfg.ModelName;

            // 선택한 모델의 그래프 축 범위 적용
            graphXMin = cfg.GraphXMin;
            graphXMax = cfg.GraphXMax;
            graphYMin = cfg.GraphYMin;
            graphYMax = cfg.GraphYMax;

            ShowCurrentModelValues();
            ResetCycleState();
            InitPlot();

             WriteCurrentModelSettingsToPlc();
            UpdateQtyLabel();
        }

        // ========================================================
        // 현재 선택된 모델 설정값을 화면에 표시
        // ========================================================
        private void ShowCurrentModelValues()
        {
            ModelConfig c = CurrentConfig;

            txtHSD.Text = c.HighDistance.ToString("0.###");
            txtLSD.Text = c.LowDistance.ToString("0.###");
            txtHS.Text = c.HighSpeed.ToString("0.###");
            txtLS.Text = c.LowSpeed.ToString("0.###");
            txtLoadSet.Text = c.LoadSet.ToString("0.###");
            txtWaitPos.Text = c.WaitPos.ToString("0.###");

            lblHSD.Text = txtHSD.Text;
            lblLSD.Text = txtLSD.Text;

            // 화면 표시만 스케일 변환
            lblHS.Text = (c.HighSpeed / 10.0).ToString("0.0");
            lblLS.Text = (c.LowSpeed / 100.0).ToString("0.00");
            

            lblLoadSet.Text = txtLoadSet.Text;
            lblWaitPos.Text = txtWaitPos.Text;
        }

        // ========================================================
        // 모델 설정 저장 버튼
        //
        // btnSave 클릭 시 실행
        // TextBox 값을 검사한 후 현재 모델에 저장한다.
        // ========================================================
        private async void btnSave_Click(object sender, EventArgs e)
        {
            double hsd;
            double lsd;
            double waitPos;
            double hs;
            double ls;
            double loadSet;

            if (!TryGetDouble(txtHSD, "고속 이송거리", out hsd) ||
                !TryGetDouble(txtLSD, "압입 이송거리", out lsd) ||
                !TryGetDouble(txtWaitPos, "대기위치", out waitPos) ||
                !TryGetDouble(txtHS, "고속 속도", out hs) ||
                !TryGetDouble(txtLS, "압입 속도", out ls) ||
                !TryGetDouble(txtLoadSet, "압입 하중", out loadSet))


            {
                return;
            }

            //if (hsd < 0 || lsd < 0 || waitPos < 0 || hs < 0 || ls < 0 || loadSet < 0) 이건 전부 음수 사용 x
            if (hs < 0 || ls < 0 || loadSet < 0) // 이건 고속속도, 압입속도, 압입하중만 음수 사용 x
            {
                MessageBox.Show("고속속도, 압입속도, 압입하중은 0 이상으로 입력하세요.");
                return;
            }

            ModelConfig c = CurrentConfig;
            c.HighDistance = hsd;
            c.LowDistance = lsd;
            c.WaitPos = waitPos;
            c.HighSpeed = hs;
            c.LowSpeed = ls;
            c.LoadSet = loadSet;

            // 저장된 현재 모델값을 메인 화면 라벨에 표시
            lblHSD.Text = c.HighDistance.ToString("0.###");
            lblLSD.Text = c.LowDistance.ToString("0.###");

            // 화면 표시만 변환
            lblHS.Text = (c.HighSpeed / 10.0).ToString("0.0");
            lblLS.Text = (c.LowSpeed / 100.0).ToString("0.00");

            lblLoadSet.Text = c.LoadSet.ToString("0.###");
            lblWaitPos.Text = c.WaitPos.ToString("0.###");
            SaveModelSettings();

             WriteCurrentModelSettingsToPlc();

            await ShowAutoCloseMessageAsync("저장 완료", 1200);
        }

        // ========================================================
        // TextBox 숫자 변환 함수
        //
        // 정상 숫자면 true
        // 숫자가 아니면 메시지 표시 후 false
        // ========================================================
        private bool TryGetDouble(TextBox box, string name, out double value)
        {
            string text = box.Text.Trim().Replace(",", ".");

            if (!double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            {
                MessageBox.Show(name + " 값을 숫자로 입력하세요.");
                box.Focus();
                box.SelectAll();
                return false;
            }

            return true;
        }

        // ========================================================
        // 전체 모델 설정 파일 저장
        //
        // 저장 위치
        // 바탕화면\CycleData\model_settings.ini
        //
        // 기존 파일을 바로 덮어쓰지 않고
        // 임시파일에 먼저 저장한 후 교체한다.
        // ========================================================
        private void SaveModelSettings()
        {
            try
            {
                Directory.CreateDirectory(saveFolderPath);
                string temp = settingFilePath + ".tmp";

                using (StreamWriter sw = new StreamWriter(temp, false))
                {
                    foreach (ModelConfig c in modelConfigs.Values.OrderBy(x => x.ModelNo))
                    {
                        sw.WriteLine(string.Join(",",
                         "MODEL",
                         c.ModelNo,
                         Escape(c.ModelName),
                         Inv(c.HighDistance),
                         Inv(c.LowDistance),
                         Inv(c.WaitPos),
                         Inv(c.HighSpeed),
                         Inv(c.LowSpeed),
                         Inv(c.LoadSet),

                         // 모델별 그래프 축 범위
                         Inv(c.GraphXMin),
                         Inv(c.GraphXMax),
                         Inv(c.GraphYMin),
                         Inv(c.GraphYMax)));

                        for (int i = 0; i < 2; i++)
                        {
                            BoxSpec b = c.Boxes[i];

                            sw.WriteLine(string.Join(",",
                                "BOX",
                                c.ModelNo,
                                i,
                                b.Use,
                                Inv(b.PosMin),
                                Inv(b.PosMax),
                                Inv(b.LoadMin),
                                Inv(b.LoadMax)));
                        }
                    }
                }

                if (File.Exists(settingFilePath))
                    File.Delete(settingFilePath);

                File.Move(temp, settingFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("설정 저장 실패\r\n" + ex.Message);
            }
        }

        // ========================================================
        // 설정 파일 불러오기
        //
        // 프로그램 시작 시 실행되며
        // 저장된 모델 및 판정구간 설정을 복원한다.
        // ========================================================
        private void LoadModelSettings()
        {
            if (!File.Exists(settingFilePath))
                return;

            try
            {
                foreach (string line in File.ReadAllLines(settingFilePath))
                {
                    string[] p = line.Split(',');

                    if (p.Length == 0)
                        continue;

                    if (p[0] == "MODEL" && p.Length >= 9)
                    {
                        int no;

                        if (!int.TryParse(p[1], out no))
                            continue;

                        ModelConfig c;

                        if (!modelConfigs.TryGetValue(no, out c))
                        {
                            c = new ModelConfig
                            {
                                ModelNo = no,

                                GraphXMin = -200,
                                GraphXMax = 200,
                                GraphYMin = 0,
                                GraphYMax = 1000,

                                Boxes = new[]
                                {
                                    new BoxSpec(true, 0, 0, 0, 0),
                                    new BoxSpec(true, 0, 0, 0, 0)
                                }
                            };

                            modelConfigs[no] = c;
                        }

                        c.ModelName = Unescape(p[2]);
                        c.HighDistance = ParseInv(p[3]);
                        c.LowDistance = ParseInv(p[4]);
                        c.WaitPos = ParseInv(p[5]);       
                        c.HighSpeed = ParseInv(p[6]);
                        c.LowSpeed = ParseInv(p[7]);
                        c.LoadSet = ParseInv(p[8]);

                        // 축 설정이 저장된 새 형식이면 불러오기
                        if (p.Length >= 13)
                        {
                            c.GraphXMin = ParseInv(p[9]);
                            c.GraphXMax = ParseInv(p[10]);
                            c.GraphYMin = ParseInv(p[11]);
                            c.GraphYMax = ParseInv(p[12]);
                        }
                        else
                        {
                            // 기존 model_settings.ini에는 축 설정이 없으므로 기본값 사용
                            c.GraphXMin = -200;
                            c.GraphXMax = 200;
                            c.GraphYMin = 0;
                            c.GraphYMax = 1000;
                        }

                    }
                    else if (p[0] == "BOX" && p.Length >= 8)
                    {
                        int no;
                        int index;

                        if (!int.TryParse(p[1], out no) ||
                            !int.TryParse(p[2], out index))
                        {
                            continue;
                        }

                        ModelConfig c;

                        if (!modelConfigs.TryGetValue(no, out c) ||
                            index < 0 ||
                            index > 1)
                        {
                            continue;
                        }

                        c.Boxes[index] = new BoxSpec(
                            bool.Parse(p[3]),
                            ParseInv(p[4]),
                            ParseInv(p[5]),
                            ParseInv(p[6]),
                            ParseInv(p[7]));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("설정 불러오기 실패\r\n" + ex.Message);
            }
        }

        private static string Inv(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static double ParseInv(string value)
        {
            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return (value ?? "").Replace("%", "%25").Replace(",", "%2C");
        }

        private static string Unescape(string value)
        {
            return (value ?? "").Replace("%2C", ",").Replace("%25", "%");
        }
    }
}
