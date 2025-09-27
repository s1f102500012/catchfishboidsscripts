using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "CatchFish/Modifier/0926/+15% crit per spike wall")]
public class CritPerSpikeWall : PlayerModifier
{
    public float perWall = 0.15f;

    static int sStacks = 0;
    static Coroutine sCo;
    static PlayerController sRunner;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        if (!sRunner) sRunner = player ? player : Object.FindFirstObjectByType<PlayerController>();
        if (sCo == null && sRunner) sCo = sRunner.StartCoroutine(Loop());
    }

    static IEnumerator Loop()
    {
        var wait = new WaitForSeconds(0.3f);
        while (true)
        {
            yield return wait;
#if UNITY_2023_1_OR_NEWER
            int count = Object.FindObjectsByType<Spike>(FindObjectsSortMode.None)?.Length ?? 0;
#else
            int count = Object.FindObjectsOfType<Spike>()?.Length ?? 0;
#endif
            float bonus = Mathf.Clamp01(count * 0.15f * sStacks);
            GameManager.CritSetBonusFromSpikes(bonus);
        }
    }

    public static void HardReset()
    {
        if (sCo != null && sRunner) sRunner.StopCoroutine(sCo);
        sCo = null; sRunner = null; sStacks = 0;
        GameManager.CritSetBonusFromSpikes(0f);
    }
}
