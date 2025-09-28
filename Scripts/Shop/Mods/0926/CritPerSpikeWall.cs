using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "CatchFish/Modifier/0926/+15% crit per spike wall")]
public class CritPerSpikeWall : PlayerModifier
{
    [Tooltip("每面尖刺墙提供的暴击加成")]
    public float perWall = 0.15f;

    static int sStacks = 0;
    static float sPerWall = 0.15f;
    static Coroutine sCo;
    static PlayerController sRunner;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        sPerWall = perWall; // 修正：使用Inspector数值
        if (!sRunner) sRunner = player ? player : Object.FindFirstObjectByType<PlayerController>();
        TryStartLoop();
    }

    static void TryStartLoop()
    {
        if (sCo == null && sRunner) sCo = sRunner.StartCoroutine(Loop());
    }

    static IEnumerator Loop()
    {
        var wait = new WaitForSeconds(0.3f);
        while (true)
        {
            // 若宿主在某帧被销毁，保证能自愈重启
            if (!sRunner)
            {
                sCo = null;
                sRunner = Object.FindFirstObjectByType<PlayerController>();
                TryStartLoop();
                yield break;
            }

#if UNITY_2023_1_OR_NEWER
            int count = Object.FindObjectsByType<Spike>(FindObjectsSortMode.None).Length;
#else
            int count = Object.FindObjectsOfType<Spike>().Length;
#endif
            float bonus = Mathf.Clamp01(count * sPerWall * sStacks); // 修正：用perWall并按叠层数放大
            GameManager.CritSetBonusFromSpikes(bonus);
            yield return wait;
        }
    }

    public static void HardReset()
    {
        if (sCo != null && sRunner) sRunner.StopCoroutine(sCo);
        sCo = null; sRunner = null; sStacks = 0;
        GameManager.CritSetBonusFromSpikes(0f);
    }

    // 可选：首场景载入即清静态，避免“禁用域重载”下的脏状态
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsOnDomainReload()
    {
        sCo = null; sRunner = null; sStacks = 0; sPerWall = 0.15f;
    }
}
