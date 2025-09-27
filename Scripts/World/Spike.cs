using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Spike : MonoBehaviour
{
    public float knockForce = 15f;

    void OnTriggerEnter2D(Collider2D other)
    {
        // 玩家：击退 + 扣分
        if (other.TryGetComponent(out PlayerController pc))
        {
            Vector2 dir = ((Vector2)pc.transform.position - (Vector2)transform.position).normalized;
            pc.Knockback(dir, knockForce);

            var gm = GameManager.Instance;
            if (gm != null)
            {
                int cur  = gm.CurrentMoney;
                int loss = Mathf.Max(1, Mathf.FloorToInt(cur * GameManager.Instance.spikePenaltyRate));
                gm.LoseMoney(loss);
            }
            return;
        }

        // 小鱼：按配置处理
        if (other.TryGetComponent(out Boid b))
        {
            var bm = FindObjectOfType<BoidManager>();
            if (!bm) { Destroy(b.gameObject); return; }

            // 道具：刺杀算玩家捕食
            if (SpikeKillsCountAsEaten.Enabled)
            {
                SpikeKillsCountAsEaten.ResolveAsPlayerEat(b);
            }
            bm.DespawnBoid(b);
        }
    }
}
