using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Gold Buff Speed 2s (Refresh only)")]
public class GoldBuffSpeedFor2s : PlayerModifier
{
    [Header("Buff Settings")]
    public float duration = 2f;        // 持续时间
    public float perItemMultiplier = 1.10f; // 每购买1个的基础倍率；总倍率=perItemMultiplier^owned

    // —— 运行期静态状态（本大局内有效）——
    static int    sOwned = 0;                 // 已购买数量
    static bool   sHooked = false;
    static float  sActiveMult = 1f;           // 当前已应用到玩家身上的倍率
    static float  sExpireAt = 0f;
    static float  sDur = 2f;                  // 记录最近一次购买的持续时间
    static float  sPerItem = 1.10f;           // 记录最近一次购买的“每件倍率”
    static PlayerController sPlayer;
    static Coroutine sCo;

    public override void Apply(PlayerController player)
    {
        // 叠加购买层数；记录参数；只注册一次事件
        sOwned = Mathf.Max(0, sOwned + 1);
        sDur = duration;
        sPerItem = perItemMultiplier;

        if (!sHooked)
        {
            GlobalEvents.OnGoldFishEaten += OnGoldFish;
            sHooked = true;
        }
    }

    static void OnGoldFish(Vector2 _)
    {
        if (!sPlayer) sPlayer = Object.FindObjectOfType<PlayerController>();
        if (!sPlayer || sOwned <= 0) return;

        float targetMult = Mathf.Pow(sPerItem, sOwned); // 例：买2个→1.1^2=1.21
        EnsureBuff(targetMult);
        sExpireAt = Time.time + sDur;

        if (sCo == null && sPlayer != null)
            sCo = sPlayer.StartCoroutine(BuffTimer());
    }

    static void EnsureBuff(float targetMult)
    {
        if (!sPlayer) return;

        if (Mathf.Abs(targetMult - sActiveMult) > 1e-4f)
        {
            // 先撤旧，再施新，避免数值漂移
            if (sActiveMult != 1f)
            {
                sPlayer.maxSpeed  /= sActiveMult;
                sPlayer.accelRate /= sActiveMult;
            }
            sActiveMult = targetMult;

            sPlayer.maxSpeed  *= sActiveMult;
            sPlayer.accelRate *= sActiveMult;
        }
        // 若目标与当前相同，则仅刷新计时（上面在 OnGoldFish 里完成）
    }

    static IEnumerator BuffTimer()
    {
        while (Time.time < sExpireAt) yield return null;

        if (sPlayer && sActiveMult != 1f)
        {
            sPlayer.maxSpeed  /= sActiveMult;
            sPlayer.accelRate /= sActiveMult;
        }
        sActiveMult = 1f;
        sCo = null;
    }

    // —— 供 GameManager 在“开新大局”时调用的硬重置 —— 
    public static void HardReset()
    {
        if (sPlayer && sActiveMult != 1f)
        {
            sPlayer.maxSpeed  /= sActiveMult;
            sPlayer.accelRate /= sActiveMult;
        }
        sActiveMult = 1f;
        sExpireAt   = 0f;
        sOwned      = 0;

        if (sCo != null && sPlayer) sPlayer.StopCoroutine(sCo);
        sCo = null;

        GlobalEvents.OnGoldFishEaten -= OnGoldFish;
        sHooked = false;
        sPlayer = null;
    }
}
