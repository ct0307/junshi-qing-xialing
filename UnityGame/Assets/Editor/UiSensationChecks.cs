using System.IO;
using UnityEngine;
using YanyiSandpan.Domain;

// 独立验收入口，复用竞速回归并将所有新截图写入本轮目录。
public static class UiSensationChecks
{
    public static void Run()
    {
        UiSensationFeedbackChecks.Verify();
        UiRaceChecks.RunTo(Path.GetFullPath("../docs/ui/runtime-sensation-20261119"));
        UiSensationFeedbackChecks.CaptureShowcase(JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text));
        File.WriteAllText(Path.Combine(UiRaceChecks.Output, "sensation-verification.json"), JsonUtility.ToJson(new Supplement {
            Status = "Passed", SlotRaycasts = UiSensationFeedbackChecks.SlotRaycasts, ShowcaseScreenshots = 12 }, true));
    }

    [System.Serializable] private sealed class Supplement
    {
        public string Status;
        public int SlotRaycasts, ShowcaseScreenshots;
        public bool RealDeviceChecked = false, HumanFunChecked = false;
    }
}
