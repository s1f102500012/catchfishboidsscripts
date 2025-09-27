using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Speed&Accel ×1.1 when over target")]
public class OverTargetSpeedAccelBoost : PlayerModifier
{
    public float multiplier = 1.10f;   // 叠乘倍率
    public float pollInterval = 0.2f;

    static bool sHooked, sApplied;
    static float sMult = 1.10f, sInv = 1f/1.10f;
    static PlayerController sPlayer;
    static Coroutine sCo;

    public override void Apply(PlayerController player)
    {
        sPlayer = player ? player : Object.FindFirstObjectByType<PlayerController>();
        sMult = Mathf.Max(1f, multiplier);
        sInv  = 1f / sMult;
        if (!sHooked && sPlayer){ sCo = sPlayer.StartCoroutine(Loop()); sHooked = true; }
    }

    static IEnumerator Loop()
    {
        var wait = new WaitForSeconds(0.2f);
        while (true)
        {
            yield return wait;
            var gm = GameManager.Instance; if (!gm) continue;

            bool cond = gm.IsRunning && gm.CurrentMoney >= GetNextTarget(gm);
            if (cond && !sApplied && sPlayer)
            {
                sPlayer.maxSpeed  *= sMult;
                sPlayer.accelRate *= sMult;
                sApplied = true;
            }
            else if (!cond && sApplied && sPlayer)
            {
                sPlayer.maxSpeed  *= sInv;
                sPlayer.accelRate *= sInv;
                sApplied = false;
            }
        }
    }

    static int GetNextTarget(GameManager gm)
    {
        // 确保 GameManager 暴露当前目标：public int NextTarget => nextTarget;
        return gm.NextTarget;
    }

    public static void HardReset()
    {
        if (sApplied && sPlayer) { sPlayer.maxSpeed *= sInv; sPlayer.accelRate *= sInv; }
        sApplied = false;
        if (sCo != null && sPlayer) sPlayer.StopCoroutine(sCo);
        sCo = null; sHooked = false; sPlayer = null;
    }
}
