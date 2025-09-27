using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/+1 Small Score, SmallsPerWave -2")]
public class SmallBaseScoreTo2 : PlayerModifier
{
    // 记录本道具对“每波小鱼数”的累计影响，便于新一大局还原
    static int sAppliedWaveDelta = 0; // 已经总共-了多少

    public override void Apply(PlayerController player)
    {
        GameManager.AddSmallScoreBonus(1);  // 仅写入加分

        var bm = BoidManager.Instance;
        if (bm)
        {
            bm.boidsPerWave = Mathf.Max(0, bm.boidsPerWave - 2);
            sAppliedWaveDelta += 2;
        }
    }

    public static void HardReset()
    {
        var bm = BoidManager.Instance;
        if (bm && sAppliedWaveDelta > 0)
        {
            bm.boidsPerWave = Mathf.Max(0, bm.boidsPerWave + sAppliedWaveDelta);
        }
        sAppliedWaveDelta = 0;
    }
}
