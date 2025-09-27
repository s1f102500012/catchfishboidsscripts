using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Shift no accel penalty")]
public class ShiftNoAccelPenalty : PlayerModifier
{
    public static bool Enabled { get; private set; }
    public override void Apply(PlayerController player) { Enabled = true; }
    public static void HardReset() { Enabled = false; }
}
