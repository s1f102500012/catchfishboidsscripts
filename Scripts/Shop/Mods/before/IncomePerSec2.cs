using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "CatchFish/Modifier/+3 Income Per Second")]
public class IncomePerSec2 : PlayerModifier
{
    static int stacks = 0;
    static Coroutine co;
    static PlayerController holder;

    public override void Apply(PlayerController player)
    {
        stacks = Mathf.Max(0, stacks + 3);
        if (!holder) holder = Object.FindObjectOfType<PlayerController>();
        if (co == null && holder) co = holder.StartCoroutine(Loop());
    }

    static IEnumerator Loop()
    {
        var gm = GameManager.Instance;
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            // 仅在局内进行时记分
            if (gm && gm.IsRunning && stacks > 0)
                gm.AddScore(stacks);    // 每层每秒+3，可叠加
            yield return wait;
        }
    }

    // 供新一大局时重置
    public static void HardReset()
    {
        stacks = 0;
        if (holder && co != null) holder.StopCoroutine(co);
        co = null; holder = null;
    }
}
