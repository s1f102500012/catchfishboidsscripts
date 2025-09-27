using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/+1 Small per round (ramp up)")]
public class RampUpSmallsPerRound : PlayerModifier
{
    static int  sStacks = 0;     // 购买层数
    static int  sAge    = 0;     // 购买后经历的小局数
    static int  sLastAdd = 0;    // 上一局临时加成（用于撤回再施加）
    static bool sHooked = false;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        if (!sHooked)
        {
            GameManager.OnMatchBegin += OnRoundBegin;
            sHooked = true;
        }
    }

    static void OnRoundBegin()
    {
        var bm = Object.FindObjectOfType<BoidManager>();
        if (!bm) return;

        // 撤销上一局的临时加成，然后计算本局
        if (sLastAdd != 0) bm.boidsPerWave = Mathf.Max(0, bm.boidsPerWave - sLastAdd);

        sAge++;                                // 本小局序号（购买之后的第 n 局）
        sLastAdd = sStacks * sAge;             // 层数 × 局序
        bm.boidsPerWave += sLastAdd;           // 本局临时加成
    }

    // 新一大局硬重置：让 GameManager 恢复基础 boidsPerWave，这里只清状态
    public static void HardReset()
    {
        sStacks = 0; sAge = 0; sLastAdd = 0;
        if (sHooked)
        {
            GameManager.OnMatchBegin -= OnRoundBegin;
            sHooked = false;
        }
    }
}
