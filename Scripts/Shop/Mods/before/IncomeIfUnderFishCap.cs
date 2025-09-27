using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "CatchFish/Modifier/+5 per second if fish < 150")]
public class IncomeIfUnderFishCap : PlayerModifier
{
    public int threshold = 150;
    public int incomePerSec = 5;

    static bool sHooked;
    static int  sThresh = 150, sIncome = 5;
    static PlayerController sRunner;
    static Coroutine sCo;

    public override void Apply(PlayerController player)
    {
        sRunner = player ? player : Object.FindFirstObjectByType<PlayerController>();
        sThresh = threshold; sIncome = incomePerSec;
        if (!sHooked && sRunner){ sCo = sRunner.StartCoroutine(Loop()); sHooked = true; }
    }

    static IEnumerator Loop()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;
            var gm = GameManager.Instance; var bm = BoidManager.Instance;
            if (!gm || !bm || !gm.IsRunning) continue;

            int count = (bm.ActiveBoids != null) ? bm.ActiveBoids.Count : 0;
            if (count < sThresh) gm.AddScore(sIncome);
        }
    }

    public static void HardReset()
    {
        if (sCo != null && sRunner) sRunner.StopCoroutine(sCo);
        sCo = null; sRunner = null; sHooked = false;
    }
}
