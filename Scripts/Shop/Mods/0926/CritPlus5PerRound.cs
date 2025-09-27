using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/0926/+5% crit each round")]
public class CritPlus5PerRound : PlayerModifier
{
    public float perRound = 0.05f;
    static int  sStacks = 0;
    static bool sHooked = false;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        if (sHooked) return;
        GameManager.OnMatchBegin += OnRoundBegin;
        sHooked = true;
    }

    static void OnRoundBegin()
    {
        GameManager.CritAddProgressBonus(0.05f * sStacks);
    }

    public static void HardReset()
    {
        if (sHooked){ GameManager.OnMatchBegin -= OnRoundBegin; sHooked = false; }
        sStacks = 0;
        GameManager.CritResetProgressBonus();
    }
}
