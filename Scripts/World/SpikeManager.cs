using UnityEngine;
using System.Collections.Generic;

public class SpikeManager : MonoBehaviour
{
    [Header("Prefab & World")]
    public GameObject spikePrefab;
    public Vector2    halfBounds = new (16f, 9f);

    [Header("Wall Segments")]
    public int longSegs  = 3;      // 上下各3段
    public int shortSegs = 2;      // 左右各2段
    [Range(1,10)] public int spikesPerRound = 4;

    readonly List<GameObject> pool = new List<GameObject>();
    readonly List<int> currentLayout = new List<int>();
    public IReadOnlyList<int> CurrentLayout => currentLayout;

    void OnEnable()  => GameManager.OnMatchBegin += SpawnByCheckpoint;
    void OnDisable() => GameManager.OnMatchBegin -= SpawnByCheckpoint;

    // —— 用 GameManager 的布局快照重刷；没有快照则随机并回填快照 —— 
    void SpawnByCheckpoint()
    {
        var gm = GameManager.Instance;
        var layout = gm != null ? gm.SpikeLayoutCheckpoint : null;
        SpawnWithLayout(layout);
        if (gm != null && (layout == null || layout.Count == 0))
            gm.SaveSpikeLayout(CurrentLayout);     // 首次随机后回填
    }

    public void SpawnWithLayout(IReadOnlyList<int> layout)
    {
        ClearAll();
        var segs = BuildSegments();                 // 10 段定义

        currentLayout.Clear();
        if (layout != null && layout.Count > 0)
        {
            // 按快照生成
            foreach (var idx in layout)
            {
                int id = Mathf.Clamp(idx, 0, segs.Count - 1);
                CreateSpike(segs[id]);
                currentLayout.Add(id);
            }
        }
        else
        {
            // 随机抽 N 段
            var chosen = new HashSet<int>();
            while (chosen.Count < Mathf.Clamp(spikesPerRound,1,segs.Count))
                chosen.Add(Random.Range(0, segs.Count));

            foreach (int id in chosen)
            {
                CreateSpike(segs[id]);
                currentLayout.Add(id);
            }
        }
    }

    // —— 清空 —— 
    public void ClearAll()
    {
        foreach (var g in pool) if (g) Destroy(g);
        pool.Clear();
    }

    // —— 段定义 + 贴框消缝（与先前版本一致）——
    List<(Vector2 pos, Vector2 size, float rot)> BuildSegments()
    {
        var list = new List<(Vector2, Vector2, float)>();
        float segX = (halfBounds.x * 2f) / longSegs;
        float segY = (halfBounds.y * 2f) / shortSegs;

        // Top & Bottom（保留你的 -0.12 / +0.12 偏移与宽度微调）
        for (int i = 0; i < longSegs; i++)
        {
            // Top（尖朝下）
            list.Add((
                new Vector2(-halfBounds.x - 0.12f + segX * (i + 0.5f),  halfBounds.y + 0.12f),
                new Vector2(segX + 0.37f, 1f),
                180f
            ));
            // Bottom（尖朝上）
            list.Add((
                new Vector2(-halfBounds.x - 0.12f + segX * (i + 0.5f), -halfBounds.y - 0.12f),
                new Vector2(segX + 0.36f, 1f),
                0f
            ));
        }

        // Left & Right（保留你让 Left=-90 / Right=90 的朝向）
        for (int i = 0; i < shortSegs; i++)
        {
            list.Add((
                new Vector2(-halfBounds.x - 0.12f, -halfBounds.y + segY * (i + 0.5f)),
                new Vector2(segY, 1f),
                -90f
            ));
            list.Add((
                new Vector2( halfBounds.x + 0.12f, -halfBounds.y + segY * (i + 0.5f)),
                new Vector2(segY, 1f),
                90f
            ));
        }
        return list;
    }   


    void CreateSpike((Vector2 pos, Vector2 size, float rot) s)
    {
        GameObject g = Instantiate(spikePrefab, s.pos, Quaternion.Euler(0,0,s.rot), transform);

        var sr  = g.GetComponent<SpriteRenderer>();
        var col = g.GetComponent<BoxCollider2D>();
        float onePx = sr && sr.sprite ? 1f / sr.sprite.pixelsPerUnit : 0.01f;

        Vector2 size = s.size + Vector2.right * onePx;   // 宽 +1px 消缝
        if (sr)  sr.size  = size;
        if (col) col.size = size;

        Vector2 inward = (Vector2)transform.position - s.pos;         // 半像素内缩
        g.transform.position = s.pos + inward.normalized * (0.5f * onePx);

        pool.Add(g);
    }
}
