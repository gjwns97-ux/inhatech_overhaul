// ========================================================
// 모델 편집 및 판정구간 설정 팝업 관리
//
// 주요 기능
// 1. 압입부/밀착부 판정구간 설정
// 2. 판정구간 사용 여부 설정
// 3. 모델명 변경
// 4. 모델 추가
// 5. 모델 삭제
// ========================================================
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace 인하테크개조
{
    public partial class Form1 : Form
    {
        private void btnCheckBoxSet_Click(object sender, EventArgs e)
        {
            ModelConfig c = CurrentConfig;

            using (Form popup = new Form())
            {
                popup.Text = c.ModelName + " 압입구간 설정";
                popup.StartPosition = FormStartPosition.CenterParent;
                popup.FormBorderStyle = FormBorderStyle.FixedDialog;
                popup.MaximizeBox = false;
                popup.MinimizeBox = false;
                popup.ClientSize = new Size(540, 315);

                string[] names = { "압입부", "밀착부" };
                CheckBox[] checks = new CheckBox[2];
                TextBox[,] inputs = new TextBox[2, 4];

                for (int i = 0; i < 2; i++)
                {
                    GroupBox group = new GroupBox
                    {
                        Text = names[i],
                        Left = 15,
                        Top = 20 + (i * 115),
                        Width = 510,
                        Height = 105
                    };

                    checks[i] = new CheckBox
                    {
                        Text = "사용",
                        Left = 15,
                        Top = 25,
                        Width = 60,
                        Checked = c.Boxes[i].Use
                    };

                    string[] captions = { "거리 MIN", "거리 MAX", "하중 MIN", "하중 MAX" };

                    for (int j = 0; j < 4; j++)
                    {
                        group.Controls.Add(new Label
                        {
                            Text = captions[j],
                            Left = 85 + (j * 103),
                            Top = 20,
                            Width = 95,
                            TextAlign = ContentAlignment.MiddleCenter
                        });

                        inputs[i, j] = new TextBox
                        {
                            Left = 90 + (j * 103),
                            Top = 50,
                            Width = 85,
                            TextAlign = HorizontalAlignment.Center
                        };

                        group.Controls.Add(inputs[i, j]);
                    }

                    inputs[i, 0].Text = c.Boxes[i].PosMin.ToString("0.###");
                    inputs[i, 1].Text = c.Boxes[i].PosMax.ToString("0.###");
                    inputs[i, 2].Text = c.Boxes[i].LoadMin.ToString("0.###");
                    inputs[i, 3].Text = c.Boxes[i].LoadMax.ToString("0.###");

                    group.Controls.Add(checks[i]);
                    popup.Controls.Add(group);
                }

                Button save = new Button
                {
                    Text = "저장",
                    Left = 165,
                    Top = 265,
                    Width = 95,
                    Height = 32
                };

                Button close = new Button
                {
                    Text = "닫기",
                    Left = 280,
                    Top = 265,
                    Width = 95,
                    Height = 32
                };

                save.Click += async (s, ev) =>
                {
                    BoxSpec[] boxes = new BoxSpec[2];

                    for (int i = 0; i < 2; i++)
                    {
                        double pmin, pmax, lmin, lmax;

                        if (!TryGetDouble(inputs[i, 0], names[i] + " 거리 MIN", out pmin) ||
                            !TryGetDouble(inputs[i, 1], names[i] + " 거리 MAX", out pmax) ||
                            !TryGetDouble(inputs[i, 2], names[i] + " 하중 MIN", out lmin) ||
                            !TryGetDouble(inputs[i, 3], names[i] + " 하중 MAX", out lmax))
                            return;

                        if (pmin > pmax || lmin > lmax)
                        {
                            MessageBox.Show(names[i] + "의 MIN은 MAX보다 클 수 없습니다.");
                            return;
                        }

                        boxes[i] = new BoxSpec(checks[i].Checked, pmin, pmax, lmin, lmax);
                    }

                    c.Boxes = boxes;
                    SaveModelSettings();
                    ShowCurrentModelValues();
                    InitPlot();
                    await ShowAutoCloseMessageAsync("구간 설정 저장 완료", 1200);
                };

                close.Click += (s, ev) => popup.Close();
                popup.Controls.Add(save);
                popup.Controls.Add(close);
                popup.ShowDialog(this);
            }
        }

        private void btnModelEdit_Click(object sender, EventArgs e)
        {
            // 이벤트가 중복 연결돼 있어도 창이 두 번 열리지 않게 방지
            if (modelEditOpening)
                return;

            modelEditOpening = true;

            try
            {
                using (Form popup = new Form())
                {
                    popup.Text = "모델 편집";
                    popup.StartPosition = FormStartPosition.CenterParent;
                    popup.FormBorderStyle = FormBorderStyle.FixedDialog;
                    popup.MaximizeBox = false;
                    popup.MinimizeBox = false;
                    popup.ClientSize = new Size(440, 330);

                    ListBox list = new ListBox
                    {
                        Left = 15,
                        Top = 15,
                        Width = 200,
                        Height = 255,
                        DisplayMember = "ModelName"
                    };

                    Label nameLabel = new Label
                    {
                        Text = "모델명",
                        Left = 235,
                        Top = 20,
                        Width = 100
                    };

                    TextBox nameBox = new TextBox
                    {
                        Left = 235,
                        Top = 45,
                        Width = 185
                    };

                    Button rename = new Button
                    {
                        Text = "이름 수정",
                        Left = 235,
                        Top = 85,
                        Width = 85
                    };

                    Button add = new Button
                    {
                        Text = "모델 추가",
                        Left = 335,
                        Top = 85,
                        Width = 85
                    };

                    Button delete = new Button
                    {
                        Text = "선택 삭제",
                        Left = 235,
                        Top = 130,
                        Width = 185
                    };

                    Button close = new Button
                    {
                        Text = "닫기",
                        Left = 170,
                        Top = 285,
                        Width = 100,
                        Height = 30
                    };

                    Action reload = () =>
                    {
                        int oldModelNo = -1;

                        ModelConfig selected =
                            list.SelectedItem as ModelConfig;

                        if (selected != null)
                            oldModelNo = selected.ModelNo;

                        list.Items.Clear();

                        foreach (ModelConfig item in
                            modelConfigs.Values.OrderBy(x => x.ModelNo))
                        {
                            list.Items.Add(item);
                        }

                        for (int i = 0; i < list.Items.Count; i++)
                        {
                            ModelConfig item =
                                list.Items[i] as ModelConfig;

                            if (item != null &&
                                item.ModelNo == oldModelNo)
                            {
                                list.SelectedIndex = i;
                                break;
                            }
                        }

                        if (list.SelectedIndex < 0 &&
                            list.Items.Count > 0)
                        {
                            list.SelectedIndex = 0;
                        }
                    };

                    list.SelectedIndexChanged += (s, ev) =>
                    {
                        ModelConfig selected =
                            list.SelectedItem as ModelConfig;

                        nameBox.Text = selected == null
                            ? ""
                            : selected.ModelName;
                    };

                    // =========================
                    // 모델 이름 수정
                    // =========================
                    rename.Click += (s, ev) =>
                    {
                        ModelConfig selected =
                            list.SelectedItem as ModelConfig;

                        if (selected == null)
                            return;

                        string newName = nameBox.Text.Trim();

                        if (string.IsNullOrWhiteSpace(newName))
                        {
                            MessageBox.Show("모델명을 입력하세요.");
                            nameBox.Focus();
                            return;
                        }

                        bool duplicate = modelConfigs.Values.Any(x =>
                            x.ModelNo != selected.ModelNo &&
                            string.Equals(
                                x.ModelName,
                                newName,
                                StringComparison.OrdinalIgnoreCase));

                        if (duplicate)
                        {
                            MessageBox.Show("같은 모델명이 이미 있습니다.");
                            nameBox.Focus();
                            nameBox.SelectAll();
                            return;
                        }

                        selected.ModelName = newName;

                        SaveModelSettings();
                        reload();
                        RefreshModelCombo();

                        lblCurrentModel.Text =
                            CurrentConfig.ModelName;
                    };

                    // =========================
                    // 모델 추가
                    // =========================
                    add.Click += async (s, ev) =>
                    {
                        int newNo = 1;

                        // 비어 있는 가장 작은 번호 사용
                        while (modelConfigs.ContainsKey(newNo))
                            newNo++;

                        modelConfigs[newNo] = CreateDefaultModel(newNo);

                        SaveModelSettings();
                        reload();
                        RefreshModelCombo();

                        // 방금 추가한 모델 선택
                        for (int i = 0; i < list.Items.Count; i++)
                        {
                            ModelConfig item = list.Items[i] as ModelConfig;

                            if (item != null && item.ModelNo == newNo)
                            {
                                list.SelectedIndex = i;
                                break;
                            }
                        }

                        await ShowAutoCloseMessageAsync(
                            "모델 추가 완료", 1200);
                    };

                    // =========================
                    // 모델 삭제
                    // =========================
                    delete.Click += (s, ev) =>
                    {
                        ModelConfig selected =
                            list.SelectedItem as ModelConfig;

                        if (selected == null)
                            return;

                        if (modelConfigs.Count <= 1)
                        {
                            MessageBox.Show(
                                "모델은 최소 1개가 필요합니다.");
                            return;
                        }

                        DialogResult result = MessageBox.Show(
                            selected.ModelName +
                            " 모델을 삭제하시겠습니까?",
                            "모델 삭제",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result != DialogResult.Yes)
                            return;

                        modelConfigs.Remove(selected.ModelNo);

                        if (!modelConfigs.ContainsKey(currentModelNo))
                        {
                            currentModelNo =
                                modelConfigs.Keys.Min();
                        }

                        SaveModelSettings();
                        reload();
                        RefreshModelCombo();
                        ShowCurrentModelValues();

                        lblCurrentModel.Text =
                            CurrentConfig.ModelName;
                    };

                    // =========================
                    // 닫기
                    // =========================
                    close.Click += (s, ev) =>
                    {
                        popup.DialogResult = DialogResult.Cancel;
                        popup.Close();
                    };

                    popup.CancelButton = close;

                    popup.Controls.Add(list);
                    popup.Controls.Add(nameLabel);
                    popup.Controls.Add(nameBox);
                    popup.Controls.Add(rename);
                    popup.Controls.Add(add);
                    popup.Controls.Add(delete);
                    popup.Controls.Add(close);

                    reload();

                    popup.ShowDialog(this);
                }
            }
            finally
            {
                // 같은 클릭 이벤트의 중복 실행이 끝난 다음 해제
                BeginInvoke(new Action(() =>
                {
                    modelEditOpening = false;
                }));
            }
        }
    }
}