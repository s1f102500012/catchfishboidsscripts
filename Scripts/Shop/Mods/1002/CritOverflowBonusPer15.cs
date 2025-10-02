using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/1002/Crit Overflow Bonus")]
public class CritOverflowBonusPer15 : PlayerModifier
{
    public float overflowStep = 0.15f;
    public float multiplierPerStep = 1f;

    static int sStacks = 0;
    static float sOverflowStep = 0.15f;
    static float sBonusPerStack = 1f;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        sOverflowStep = Mathf.Max(0.0001f, overflowStep);
        sBonusPerStack = multiplierPerStep;
        RefreshBonus();
    }

    static void RefreshBonus()
    {
        if (sStacks > 0 && sOverflowStep > 0f && sBonusPerStack != 0f)
            GameManager.CritSetOverflowBonus(sOverflowStep, sBonusPerStack * sStacks);
        else
            GameManager.CritSetOverflowBonus(0f, 0f);
    }

    public static void HardReset()
    {
        sStacks = 0;
        sOverflowStep = 0.15f;
        sBonusPerStack = 1f;
        GameManager.CritSetOverflowBonus(0f, 0f);
    }
}
