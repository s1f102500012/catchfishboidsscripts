using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Spawn 3 Small On Gold")]
public class Spawn3SmallOnGold : PlayerModifier
{
    [Header("Spawn Behind Player")]
    public float backOffset = 1.2f;        // 离玩家身后的基准距离
    public float sideSpread = 0.8f;        // 左右随机偏移
    [Range(0f,1f)] public float initialSpeedFrac = 0.6f; // 初速度占小鱼maxSpeed的比例
    public float margin = 0.5f;            // 贴边安全边距

    public override void Apply(PlayerController player)
    {
        GlobalEvents.OnGoldFishEaten += _ => Handle();
    }

    void Handle()
    {
        var mgr = Object.FindObjectOfType<BoidManager>();
        if (!mgr || !mgr.boidPrefab) return;

        var pTr = mgr.Player ? mgr.Player : Object.FindObjectOfType<PlayerController>()?.transform;
        if (!pTr) return;

        for (int i = 0; i < 1; i++)
        {
            Vector2 spawnPos;
            Vector2 fleeDir;
            GetBehind(mgr, pTr, out spawnPos, out fleeDir);

            Boid b = Object.Instantiate(mgr.boidPrefab, spawnPos, Quaternion.identity, mgr.transform);
#if UNITY_2023_1_OR_NEWER
            b.GetComponent<Rigidbody2D>().linearVelocity = fleeDir * b.maxSpeed * initialSpeedFrac;
#else
            b.GetComponent<Rigidbody2D>().velocity       = fleeDir * b.maxSpeed * initialSpeedFrac;
#endif
            mgr.ActiveBoids.Add(b);
        }
    }

    void GetBehind(BoidManager mgr, Transform pTr, out Vector2 pos, out Vector2 fleeDir)
    {
        Vector2 back = -(Vector2)pTr.up;                               // 身后方向
        Vector2 side = new Vector2(-back.y, back.x);                   // 侧向
        pos = (Vector2)pTr.position + back * backOffset +
              side * Random.Range(-sideSpread, sideSpread);

        // 限制在边界内
        Vector2 half = mgr.spawnArea;
        pos = new Vector2(
            Mathf.Clamp(pos.x, -half.x + margin, half.x - margin),
            Mathf.Clamp(pos.y, -half.y + margin, half.y - margin)
        );

        fleeDir = (pos - (Vector2)pTr.position).normalized;            // 远离玩家
        if (fleeDir.sqrMagnitude < 1e-4f) fleeDir = back;
    }
}
