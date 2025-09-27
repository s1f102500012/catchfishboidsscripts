using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuBackground : MonoBehaviour
{
    [Header("Prefabs & Links")]
    public Boid boidPrefab;
    public GameObject predatorSpritePrefab;
    public Transform[] spawnPoints;

    [Header("Tuning")]
    public Vector2 spawnArea = new(16, 9);
    public int boidsPerWave = 12;
    public float interval = 10f;
    public int maxBoids = 200;

    [Header("Safety")]
    public float predatorSafeDist = 4f;      // 生成点需远离捕食者

    // —— runtime —— 
    BoidManager bm;
    Transform origPlayer;
    PlayerController realPlayer;
    bool realPlayerActive;
    GameObject predator;
    Coroutine loopCo;
    readonly List<Boid> menuBoids = new();
    Collider2D realPlayerCol;
    Rigidbody2D realPlayerRb;
    bool savedCtrlEnabled, savedColEnabled, savedRbSim;


    void OnEnable()
    {
        if (!Application.isPlaying) return;

        Time.timeScale = 1f;
        bm = FindFirstObjectByType<BoidManager>();
        if (!bm) return;

        // 冻结真实玩家（不要 SetActive(false)）
    #if UNITY_2023_1_OR_NEWER
        realPlayer = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    #else
        realPlayer = FindFirstObjectByType<PlayerController>();
    #endif
        if (realPlayer)
        {
            realPlayerCol = realPlayer.GetComponent<Collider2D>();
            realPlayerRb  = realPlayer.GetComponent<Rigidbody2D>();

            savedCtrlEnabled = realPlayer.enabled;
            savedColEnabled  = realPlayerCol ? realPlayerCol.enabled : false;
            savedRbSim       = realPlayerRb  ? realPlayerRb.simulated : false;

            realPlayer.enabled = false;
            if (realPlayerCol) realPlayerCol.enabled = false;
            if (realPlayerRb) { realPlayerRb.linearVelocity = Vector2.zero; realPlayerRb.simulated = false; }
            // 仍保持 GameObject 处于激活，避免后续找不到
        }

        // 生成 AI 捕食者，并把 BoidManager 的 Player 指向它
        if (!predator && predatorSpritePrefab)
        {
            predator = Instantiate(predatorSpritePrefab, Vector3.zero, Quaternion.identity, transform);
            var ai = predator.GetComponent<MenuPredator>() ?? predator.AddComponent<MenuPredator>();
            ai.Init(this);
            var prb = predator.GetComponent<Rigidbody2D>();
            if (prb) { prb.linearVelocity = Vector2.zero; prb.position = Vector2.zero; }
        }

        origPlayer = bm.Player;
        if (predator) bm.Player = predator.transform;

        if (loopCo == null) loopCo = StartCoroutine(SpawnLoop());
    }

    void OnDisable()
    {
        if (loopCo != null) { StopCoroutine(loopCo); loopCo = null; }

        // 用 BoidManager 的接口清掉菜单鱼
        if (bm)
        {
            for (int i = 0; i < menuBoids.Count; i++)
                if (menuBoids[i]) bm.DespawnBoid(menuBoids[i]);
            bm.Player = origPlayer;
        }
        menuBoids.Clear();

        if (predator) { Destroy(predator); predator = null; }

        // 恢复真实玩家
        if (realPlayer)
        {
            realPlayer.enabled = savedCtrlEnabled;
            if (realPlayerCol) realPlayerCol.enabled = savedColEnabled;
            if (realPlayerRb)  { realPlayerRb.simulated = savedRbSim; realPlayerRb.linearVelocity = Vector2.zero; realPlayerRb.position = Vector2.zero; }
            realPlayer.gameObject.SetActive(true);   // 保底
        }
    }

    IEnumerator SpawnLoop()
    {
        yield return null;
        SpawnWave();
        var wait = new WaitForSeconds(interval);
        while (true) { yield return wait; SpawnWave(); }
    }

    void SpawnWave()
    {
        if (!bm) return;
        if (menuBoids.Count >= maxBoids) return;

        Transform sp = ChooseSafeSpawnPoint();
        for (int i = 0; i < boidsPerWave; i++)
        {
            Vector2 pos = sp
                ? (Vector2)sp.position + Random.insideUnitCircle * 0.7f
                : Random.insideUnitCircle * new Vector2(spawnArea.x, spawnArea.y) * 0.6f;

            Boid b = Instantiate(boidPrefab, pos, Quaternion.identity, bm.transform);
            b.isGolden = false;

            // 全局乘子与初速度
            b.maxSpeed *= bm.globalSpeedMult;
            b.maxForce *= bm.globalForceMult;

#if UNITY_2023_1_OR_NEWER
            var rb = b.GetComponent<Rigidbody2D>();
            rb.linearVelocity = Random.insideUnitCircle * b.maxSpeed * 0.35f;
#else
            var rb = b.GetComponent<Rigidbody2D>();
            rb.velocity = Random.insideUnitCircle * b.maxSpeed * 0.35f;
#endif
            bm.ActiveBoids.Add(b);
            menuBoids.Add(b);
        }
    }

    Transform ChooseSafeSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;
        if (!predator) return spawnPoints[Random.Range(0, spawnPoints.Length)];

        Vector2 p = predator.transform.position;
        Transform best = null;
        float bestD2 = -1f;
        List<Transform> safe = null;

        // 筛安全点
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var s = spawnPoints[i]; if (!s) continue;
            float d2 = ((Vector2)s.position - p).sqrMagnitude;
            if (d2 >= predatorSafeDist * predatorSafeDist)
            {
                (safe ??= new List<Transform>()).Add(s);
            }
            if (d2 > bestD2) { bestD2 = d2; best = s; }
        }
        if (safe != null && safe.Count > 0) return safe[Random.Range(0, safe.Count)];
        return best; // 全不安全时取最远
    }
    
    public void Show() { enabled = true; }
    public void Hide() { enabled = false; }

}
