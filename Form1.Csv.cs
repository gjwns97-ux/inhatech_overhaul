// ========================================================
// CSV 데이터 저장 및 데이터 폴더 열기
//
// 주요 기능
// 1. 사이클 종료 시 검사 결과 CSV 저장
// 2. 월별 폴더 자동 생성
// 3. 모델별·날짜별 CSV 파일 생성
// 4. 모델명에 특수문자가 있어도 안전하게 저장
// 5. 데이터 저장 폴더 열기
// ========================================================
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace 인하테크개조
{
    public partial class 
        Form1 : Form
    {

        // ========================================================
        // 현재 사이클 결과 CSV 저장
        //
        // FinishCycleJudge()에서 최종 판정 후 호출
        //
        // 저장 위치 예시
        // 바탕화면\CycleData\2026-07\
        // MODEL_1_2026-07-25.csv
        // ========================================================
        private void SaveCycleData()
                {
                    try
                    {
                        string monthFolder = Path.Combine(
                            saveFolderPath, DateTime.Now.ToString("yyyy-MM"));
        
                        Directory.CreateDirectory(monthFolder);
        
                        string safeName = CurrentConfig.ModelName;
                        foreach (char c in Path.GetInvalidFileNameChars())
                            safeName = safeName.Replace(c.ToString(), "");
        
                        string filePath = Path.Combine(
                            monthFolder,
                            safeName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv");
        
                        bool newFile = !File.Exists(filePath);
        
                        using (StreamWriter sw = new StreamWriter(
                            filePath, true, System.Text.Encoding.UTF8))
                        {
                            if (newFile)
                            {
                                sw.WriteLine(
                                    "시간,모델명,고속하강위치[mm],시트압입위치[mm],고속속도[mm/sec],압입속도[mm/sec],압입하중[kgf]," +
                                    "압입부거리MIN[mm],압입부거리MAX[mm],압입부하중MIN[kgf],압입부하중MAX[kgf]," +
                                    "밀착부거리MIN[mm],밀착부거리MAX[mm],밀착부하중MIN[kgf],밀착부하중MAX[kgf]," +
                                    "최종거리[mm],최대하중[kgf],총생산량[EA],양품생산량[EA],불량발생량[EA],판정");
                            }
        
                            double finalPos = servoX.Count > 0 ? servoX.Last() : 0;
                            double maxLoad = loadY.Count > 0 ? loadY.Max() : 0;
                            ModelConfig c = CurrentConfig;

                            // CSV 저장 직전 PLC 최신 생산량 읽기
                            int saveTotalQty = plc.ReadWord(ADDR_TOTAL_QTY);
                            int savePassQty = plc.ReadDWord(ADDR_PASS_QTY);
                            int saveNgQty = plc.ReadDWord(ADDR_NG_QTY);

                    sw.WriteLine(string.Join(",",
                                 "'" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                Csv(c.ModelName),
                                Inv(c.HighDistance), Inv(c.LowDistance),
                                Inv(c.HighSpeed), Inv(c.LowSpeed), Inv(c.LoadSet),
                                Inv(c.Boxes[0].PosMin), Inv(c.Boxes[0].PosMax),
                                Inv(c.Boxes[0].LoadMin), Inv(c.Boxes[0].LoadMax),
                                Inv(c.Boxes[1].PosMin), Inv(c.Boxes[1].PosMax),
                                Inv(c.Boxes[1].LoadMin), Inv(c.Boxes[1].LoadMax),
                                Inv(finalPos), Inv(maxLoad),
                                saveTotalQty,
                                savePassQty,
                                saveNgQty,
                                lastJudgeOk ? "OK" : "NG"));
                }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("CSV 저장 실패\r\n" + ex.Message);
                    }
                }

                private static string Csv(string value)
                {
                    value = value ?? "";
        
                    if (value.Contains(",") || value.Contains("\""))
                        return "\"" + value.Replace("\"", "\"\"") + "\"";
        
                    return value;
                }

                private void btnOpenFile_Click(object sender, EventArgs e)
                {
                    try
                    {
                        Directory.CreateDirectory(saveFolderPath);
                        Process.Start("explorer.exe", saveFolderPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("데이터 폴더 열기 실패\r\n" + ex.Message);
                    }
                }
    }
}
