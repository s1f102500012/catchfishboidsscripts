using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/+0.5% Crit per Small Fish (per round, capped 50%)")]
public class IncrementalCritPerSmallFish : PlayerModifier
{
    public float perFish = 0.005f; // 每条小鱼 +0.5%

    static float sAccum = 0f;
    static bool  sHooked = false;

    public override void Apply(PlayerController player)
    {
        if (sHooked) return;
        GlobalEvents.OnSmallFishEaten += OnSmallFish;
        GameManager.OnMatchBegin      += OnRoundBegin;
        sHooked = true;
    }

    static void OnSmallFish(Vector2 _)
    {
        sAccum = Mathf.Min(0.50f, sAccum + 0.005f); // 封顶 50%
        GameManager.CritSetDynamicChance(sAccum);
    }

    static void OnRoundBegin()
    {
        sAccum = 0f;
        GameManager.CritSetDynamicChance(0f);
    }

    public static void HardReset()
    {
        sAccum = 0f;
        GameManager.CritSetDynamicChance(0f);
        if (sHooked)
        {
            GlobalEvents.OnSmallFishEaten -= OnSmallFish;
            GameManager.OnMatchBegin      -= OnRoundBegin;
            sHooked = false;
        }
    }
}
