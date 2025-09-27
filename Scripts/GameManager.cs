using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static event System.Action OnMatchBegin;

    /* ===== 时间与经济 ===== */
    [Header("Durations")]
    public float matchTime = 40f;
    public float stopSpawnBeforeEnd = 10f;

    [Header("Economy")]
    public int firstTarget = 100;
    public float multiplier = 1.5f;   // legacy, unused for target

    // 新目标计算权重：Next = Prev*targetPrevWeight + Bank*targetBankWeight
    public float targetPrevWeight = 1.25f;
    public float targetBankWeight = 0.15f;


    /* ===== 玩家出生点 ===== */
    [Header("Player Spawn")]
    public Vector2 playerSpawn = Vector2.zero;

    /* ===== UI ===== */
    [Header("UI Panels")]
    public GameObject mainMenuPanel, hudPanel, shopPanel, pausePanel, gameOverPanel;

    [Header("HUD Text")]
    public TMP_Text scoreText, timerText, hudTargetText;

    [Header("Shop Text")]
    public TMP_Text shopTargetText, availText;

    [Header("Pause Text")]
    public TMP_Text pauseStatsText;          // 显示速度/加速度

    [Header("Main Menu Buttons (Optional)")]
    public Button mainMenuContinueButton;    // 可不填；若填则自动控制 interactable

    [Header("Game Over Text")]
    public TMP_Text finalScoreText;

    [Header("Baselines (auto)")]
    bool baselineCaptured = false;
    float basePlayerMaxSpeed, basePlayerAccelRate;
    Vector3 basePlayerScale;
    int baseBoidsPerWave;
    float baseGoldenChance;

    // 放到你原来“unityroom Ranking”区
    public enum WriteMode { HighScoreDesc, HighScoreAsc, Always }

    [Header("unityroom Ranking")]
    public int boardRunNo = 1;                 // 单局榜
    public int boardCumNo = 2;                 // 累计总分榜
    public WriteMode writeModeRun = WriteMode.HighScoreDesc;
    public WriteMode writeModeCum = WriteMode.HighScoreDesc;

    const string PP_CUM = "UR_TotalScore_V1";  // 本地累计存档键


    [Header("Rules")]
    [Range(0f,1f)] public float spikePenaltyRate = 0.25f;  // 默认25%

    [Header("Menu Background")]
    public MenuBackground menuBackground;

    [Header("Auto Play")]
    public Toggle autoPlayToggle;
    const string PP_AUTO = "autoPlay_v2";                 // ← 原来是 "autoPlay"
    public bool AutoPlay => PlayerPrefs.GetInt(PP_AUTO, 0) == 1;

    #if UNITY_EDITOR
    [Header("Debug Counters (Editor Only)")]
    public TMP_Text whiteCountText;
    public TMP_Text goldCountText;
    float _countTick = 0f;                 // 可选：降低统计频率
    #endif
    
    /* ===== Crit Core ===== */
    [Header("Crit Core")]
    [Range(0f,1f)] public float critFixedChance = 0f;
    [Range(0f,1f)] public float critDynamicChance = 0f;
    [Min(1f)]      public float critMultiplier   = 2f;
    // ★ 新增两个来源
    [Range(0f,1f)] public float critBonusFromSpikes    = 0f;
    [Range(0f,1f)] public float critBonusFromProgress  = 0f;

    // ★ 修改总暴击率
    public float CritChance =>
        Mathf.Clamp01(critFixedChance + critDynamicChance + critBonusFromSpikes + critBonusFromProgress);

    // 供 Mods 调用
    public static void CritSetBonusFromSpikes(float v)
    {
        if (Instance) Instance.critBonusFromSpikes = Mathf.Clamp01(v);
    }
    public static void CritAddProgressBonus(float d)
    {
        if (Instance) Instance.critBonusFromProgress =
            Mathf.Clamp01(Instance.critBonusFromProgress + Mathf.Max(0f, d));
    }
    public static void CritResetProgressBonus()
    {
        if (Instance) Instance.critBonusFromProgress = 0f;
    }

    public static void CritAddMultiplier(float delta)
    {
        if (Instance) Instance.critMultiplier = Mathf.Max(1f, Instance.critMultiplier + delta);
    }


    // 暴击事件（供道具监听）
    public static event Action OnCrit;

    // 供 Mods 调用的接口
    public static void CritAddFixedChance(float d) { if (Instance) Instance.critFixedChance  = Mathf.Clamp01(Instance.critFixedChance + Mathf.Max(0f,d)); }
    public static void CritSetDynamicChance(float v){ if (Instance) Instance.critDynamicChance = Mathf.Max(0f,v); }
    public static void CritResetModel()
    {
        if (!Instance) return;
        Instance.critFixedChance = 0f;
        Instance.critDynamicChance = 0f;
        Instance.critMultiplier = 2f;
        Instance.critBonusFromSpikes = 0f;     // ★
        Instance.critBonusFromProgress = 0f;   // ★
    }



    // ===== Score Floors =====
    [Header("Score Floors")]
    public int smallScoreFloor = 0;
    public int goldScoreFloor  = 0;
    public static void SetSmallScoreFloor(int v){ if (Instance) Instance.smallScoreFloor = Mathf.Max(0,v); }
    public static void SetGoldScoreFloor (int v){ if (Instance) Instance.goldScoreFloor  = Mathf.Max(0,v); }

    [Header("Score Adders")]
    public int smallScoreAdd = 0;   // 小鱼额外加分（叠加）
    public int goldScoreAdd  = 0;   // 金鱼额外加分（叠加）

    public static void AddSmallScoreBonus(int delta)
    {
        if (Instance) Instance.smallScoreAdd += Mathf.Max(0, delta);
    }
    public static void AddGoldScoreBonus(int delta)
    {
        if (Instance) Instance.goldScoreAdd += Mathf.Max(0, delta);
    }
    public static void ResetScoreAdders()   // 新一大局时清零
    {
        if (!Instance) return;
        Instance.smallScoreAdd = 0;
        Instance.goldScoreAdd  = 0;
    }



    /* ===== 状态 ===== */
    enum State { Menu, Play, Shop, Pause, GameOver }
    State state = State.Menu;
    State pausedFrom = State.Menu;           // 记录从哪里进入 Pause
    State suspendedFrom = State.Menu;        // 记录从哪里“回主菜单”（供 Continue 使用）
    bool hasSuspended = false;              // 主菜单 Continue 是否可用

    float timeLeft;
    int currency;          // 可消费余额（跨回合保留）
    int totalEarned;       // 本 run 获得总额（不减购买）
    int targetScore;
    int currencyCheckpoint;      // 本小局开始时的余额
    int totalEarnedCheckpoint;   // 本小局开始时的已获得总分（用于丢弃被中断小局的收益）

    // —— Spike 布局快照（用于 Continue 保持一致）——
    private List<int> spikeLayoutCheckpoint = new List<int>();
    public IReadOnlyList<int> SpikeLayoutCheckpoint => spikeLayoutCheckpoint;

    public void SaveSpikeLayout(IReadOnlyList<int> layout)
    {
        spikeLayoutCheckpoint = layout != null ? new List<int>(layout) : new List<int>();
    }
    public void ClearSpikeLayoutCheckpoint() => spikeLayoutCheckpoint.Clear();


    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!PlayerPrefs.HasKey(PP_AUTO)) { PlayerPrefs.SetInt(PP_AUTO, 0); PlayerPrefs.Save(); }
        if (PlayerPrefs.HasKey("autoPlay")) PlayerPrefs.DeleteKey("autoPlay");   // 兼容清理
    }


    void Start()
    {
        CaptureBaselines();               // 先记录基线
        EnterMenu(hardReset: true);       // 再进入菜单/初始化
        WireCritListener();
    }

    void Update()
    {
        // ESC 切换暂停：Play/Shop → Pause；Pause → Continue
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (state == State.Play) EnterPause(State.Play);
            else if (state == State.Shop) EnterPause(State.Shop);
            else if (state == State.Pause) OnPauseContinue();
        }

        if (state != State.Play) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) { timeLeft = 0f; EnterShop(); }
        RefreshHUD();

        #if UNITY_EDITOR
        // 只在局内显示；把 0.2f 改成你想要的采样周期
        if (IsRunning)
        {
            _countTick += Time.deltaTime;
            if (_countTick >= 0.2f) { _countTick = 0f; UpdateFishCounters(); }
        }
        #endif
    }

    void SaveRoundCheckpoint()
    {
        currencyCheckpoint = currency;
        totalEarnedCheckpoint = totalEarned;
    }

    void RestoreRoundCheckpoint()
    {
        currency = currencyCheckpoint;
        totalEarned = totalEarnedCheckpoint;
    }


    /* ===== 公共接口 ===== */
    public bool IsRunning => state == State.Play;
    public float TimeLeft => timeLeft;
    public int CurrentMoney => currency;

    public int NextTarget => targetScore;   // 或你的实际字段名

    public void AddScore(int delta)
    {
        currency += delta;
        totalEarned += delta;
        RefreshHUD();
    }

    public bool Spend(int cost)
    {
        if (currency < cost) return false;
        currency -= cost;
        return true;
    }


    /* ===== UI 回调 ===== */
    // 主菜单：Play = 新开一局；Continue = 恢复（若有挂起）
    public void OnPlay()
    {
        NewRunReset();
        EnterPlay();
        hasSuspended = false;    // 新开局不算“挂起”
        ResetRunUpgrades();
        // —— 道具与商店 —— 
        ShopManager.ResetPriceScaling();
        ShopManager.ResetRunBans();
        ShopManager.ResetRerollCost();
        ClearSpikeLayoutCheckpoint();
        GoldBuffSpeedFor2s.HardReset();
        spikePenaltyRate = 0.25f;                // 恢复默认25%
        var bm = FindObjectOfType<BoidManager>();
        if (bm) bm.extraGoldenPerWave = 0;       // 额外金鱼清零
        IncomePerSec1.HardReset();
        IncomePerSec2.HardReset();
        CritResetModel();   // 清内建几率/外部几率/倍率→×2
        InterestEvery5s.HardReset();
        RampUpSmallsPerRound.HardReset();
        SpikeKillsCountAsEaten.HardReset();
        EatNearestOnGold.HardReset();
        ShiftNoAccelPenalty.HardReset();
        OverTargetSpeedAccelBoost.HardReset();
        IncomeIfUnderFishCap.HardReset();
        GoldBounceSplitSmall.HardReset();
        EatOnAnyBounce.HardReset();
        GoldChanceFromSpeed.HardReset();
        GoldSplitOnSmallBounce.HardReset();
        GoldBounceSplitGold.HardReset();
        SmallBounceSplitSmall.HardReset();
        smallScoreFloor = 0;
        goldScoreFloor  = 0;
        CritToSmallsPerWave.HardReset();
        ResetScoreAdders();
        SmallBaseScoreTo2.HardReset();
        SpikeWallsMinusOne.HardReset();
        SpikeWallsPlusOneSpeed.HardReset();
        CritPerSpikeWall.HardReset();
        CritPlus5PerRound.HardReset();


        if (bm) { bm.globalSpeedMult = 1f; bm.globalForceMult = 1f; }
        currency = 0; totalEarned = 0; targetScore = firstTarget;
    }
    public void OnMainMenuContinue()
    {
        if (!hasSuspended) return;
        if (suspendedFrom == State.Shop)
        {
            state = State.Shop;
            Time.timeScale = 0f;
            mainMenuPanel.SetActive(false);
            hudPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            pausePanel.SetActive(false);
            shopPanel.SetActive(true);          // 直接回到之前的商店，不重新 Roll
            if (shopTargetText) shopTargetText.text = $"{targetScore}";
            if (availText) availText.text = $"{currency}";
        }
        else // 从 Play 返回的 → 重开本小局
        {
            RestoreRoundCheckpoint();            // ★ 先把余额/总分还原到小局开局时
            EnterPlay();                         // 重开一局
        }
        hasSuspended = false;
        UpdateContinueButton();
        ApplyAutoPlayState();
    }

    // 商店页的 NEXT ROUND 按钮
    public void OnNextRound()
    {
        // 只允许在商店里触发
        if (state != State.Shop) return;

        // 退出挂起状态，防止主菜单 Continue 仍然点亮
        hasSuspended = false;
        UpdateContinueButton();

        // 隐藏商店 → 开始下一局（会 Time.timeScale=1、清场并复位玩家）
        shopPanel.SetActive(false);
        ClearSpikeLayoutCheckpoint();   // ★ 新小局：先清快照
        EnterPlay();
    }

    public void OnMainMenu() => EnterMenu(hardReset: true);   // 结算面板按钮

    // Pause 面板：Continue / Main Menu
    public void OnPauseContinue()
    {
        if (state != State.Pause) return;
        pausePanel.SetActive(false);
        if (pausedFrom == State.Play)
        {
            state = State.Play;
            hudPanel.SetActive(true);
            Time.timeScale = 1f;
        }
        else // pausedFrom == Shop
        {
            state = State.Shop;
            shopPanel.SetActive(true);
            Time.timeScale = 0f;
        }
    }
    public void OnPauseMainMenu()
    {
        if (state != State.Pause) return;
        // 记录挂起来源，进入主菜单但不重置本次 run
        suspendedFrom = pausedFrom;
        hasSuspended = true;
        UpdateContinueButton();
        EnterMenu(hardReset: false);
    }

    public void LoseMoney(int amount)
    {
        int loss = Mathf.Clamp(amount, 0, currency);
        if (loss <= 0) return;
        currency -= loss;
        RefreshHUD();
    }


    public void Quit()
    {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
            Application.Quit();
    #endif
    }

    /* ================== 状态流转 ================== */
    // 进入主菜单：hardReset=true 清空本次 run；false 保留（用于 Pause→MainMenu）
    void EnterMenu(bool hardReset)
    {
        CaptureBaselines();
        Time.timeScale = 1f;
        state = State.Menu;
        ClearScene(); ResetPlayerToSpawn();
        if (autoPlayToggle) autoPlayToggle.SetIsOnWithoutNotify(AutoPlay);  // ← 不触发 OnValueChanged
        if (hardReset){ PlayerPrefs.SetInt("autoPlay_v2", 0); PlayerPrefs.Save(); }
        if (autoPlayToggle) autoPlayToggle.SetIsOnWithoutNotify(AutoPlay);

        if (hardReset)
        {
            // ★ 新增：清空所有增益/道具效果
            ResetRunUpgrades();
            // —— 道具与商店 —— 
            ShopManager.ResetPriceScaling();
            ShopManager.ResetRunBans();
            ShopManager.ResetRerollCost();
            ClearSpikeLayoutCheckpoint();
            GoldBuffSpeedFor2s.HardReset();
            spikePenaltyRate = 0.25f;                // 恢复默认25%
            var bm = FindObjectOfType<BoidManager>();
            if (bm) bm.extraGoldenPerWave = 0;       // 额外金鱼清零
            IncomePerSec1.HardReset();
            IncomePerSec2.HardReset();
            CritResetModel();   // 清内建几率/外部几率/倍率→×2
            InterestEvery5s.HardReset();
            RampUpSmallsPerRound.HardReset();
            SpikeKillsCountAsEaten.HardReset();
            EatNearestOnGold.HardReset();
            ShiftNoAccelPenalty.HardReset();
            OverTargetSpeedAccelBoost.HardReset();
            IncomeIfUnderFishCap.HardReset();
            GoldBounceSplitSmall.HardReset();
            EatOnAnyBounce.HardReset();
            GoldChanceFromSpeed.HardReset();
            GoldSplitOnSmallBounce.HardReset();
            GoldBounceSplitGold.HardReset();
            SmallBounceSplitSmall.HardReset();
            smallScoreFloor = 0;
            goldScoreFloor  = 0;
            CritToSmallsPerWave.HardReset();
            ResetScoreAdders();
            SmallBaseScoreTo2.HardReset();
            SpikeWallsMinusOne.HardReset();
            SpikeWallsPlusOneSpeed.HardReset();
            CritPerSpikeWall.HardReset();
            CritPlus5PerRound.HardReset();


            // 另外把 BoidManager 的全局乘子复原：
            if (bm) { bm.globalSpeedMult = 1f; bm.globalForceMult = 1f; }

            currency = 0; totalEarned = 0; targetScore = firstTarget;
            hasSuspended = false;
        }

        Time.timeScale = 1f;
        state = State.Menu;

        // 统一隐藏
        mainMenuPanel.SetActive(true);
        hudPanel.SetActive(false);
        shopPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        pausePanel.SetActive(false);

        // 清场（不影响商店 UI 内容）
        ClearScene();
        ResetPlayerToSpawn();

        if (hardReset)
        {
            currency = 0;
            totalEarned = 0;
            targetScore = firstTarget;
            hasSuspended = false;
        }
        UpdateContinueButton();
        RefreshHUD(); // 刷新一次 HUD 文本（即使 HUD 隐藏）
        if (menuBackground) menuBackground.Show();
    }

    // 新开一局前重置可变量（不清菜单）
    void NewRunReset()
    {
        timeLeft = matchTime;
        // currency/totalEarned/targetScore 不在此处清零，由 EnterMenu(hardReset:true) 负责
    }

    void EnterPlay()
    {
        Time.timeScale = 1f;
        state = State.Play;

        ClearScene();
        ResetPlayerToSpawn();

        NewRunReset();                 // 你已有的行
        SaveRoundCheckpoint();         // ★ 新增：记录本小局“开局余额/总分”

        mainMenuPanel.SetActive(false);
        hudPanel.SetActive(true);
        shopPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        pausePanel.SetActive(false);

        RefreshHUD();
        OnMatchBegin?.Invoke();
        if (menuBackground) menuBackground.Hide();

        // —— 强制确保玩家存在、激活、复位，并交还给 BoidManager —— 
        var pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc)
        {
            if (!pc.gameObject.activeSelf) pc.gameObject.SetActive(true);
            pc.enabled = true;

            var rb = pc.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.simulated = true;
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints2D.None;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.position = playerSpawn;                 // 你的开局坐标
            }

            // 交还“玩家引用”给 BoidManager
            var bm = BoidManager.Instance;
            if (bm) bm.Player = pc.transform;
        }
        ApplyAutoPlayState();
    }

    void EnterShop()
    {
        state = State.Shop;
        Time.timeScale = 0f;

        int prevTarget = targetScore;
        int bankAtShop = currency; // 以进店瞬间余额作为参与值

        if (bankAtShop < prevTarget) { EnterGameOver(); return; }

        // Next = Prev*1.25 + Bank*0.15（可在 Inspector 调整两个权重）
        targetScore = Mathf.CeilToInt(prevTarget * targetPrevWeight + bankAtShop * targetBankWeight);

        hudPanel.SetActive(false);
        pausePanel.SetActive(false);
        shopPanel.SetActive(true);

        if (shopTargetText) shopTargetText.text = $"{targetScore}";
        if (availText) availText.text = $"{currency}";

        // 只有首次进入商店才 Roll。若是主菜单 Continue 回来，这里不会被调用。
        var sm = shopPanel.GetComponent<ShopManager>();
        // if (sm && !hasSuspended) sm.OpenShop();
        if (menuBackground) menuBackground.Hide();
        if (sm)
        {
            // 非挂起 → 一定 Roll；挂起但页面是空的（只有占位文本） → 也 Roll
            if (!hasSuspended || sm.IsPageEmpty())
                sm.OpenShop();
        }       
    }

    void EnterGameOver()
    {
        state = State.GameOver;
        Time.timeScale = 1f;

        hudPanel.SetActive(false);
        shopPanel.SetActive(false);
        pausePanel.SetActive(false);
        gameOverPanel.SetActive(true);

        if (finalScoreText) finalScoreText.text = $"Total Score\n{totalEarned}";
        hasSuspended = false;
        UpdateContinueButton();

        int runScore = totalEarned;                                        // 本局总得分
        int cum = PlayerPrefs.GetInt(PP_CUM, 0) + runScore;                // 本地累计
        PlayerPrefs.SetInt(PP_CUM, cum);
        PlayerPrefs.Save();

        #if UNITY_WEBGL && !UNITY_EDITOR
        unityroom.Api.UnityroomApiClient.Instance.SendScore(
            boardRunNo, (float)runScore, (unityroom.Api.ScoreboardWriteMode)writeModeRun);
        unityroom.Api.UnityroomApiClient.Instance.SendScore(
            boardCumNo, (float)cum,      (unityroom.Api.ScoreboardWriteMode)writeModeCum);
        #endif

    }

    void EnterPause(State from)
    {
        pausedFrom = from;
        state = State.Pause;
        Time.timeScale = 0f;

        if (from == State.Play) hudPanel.SetActive(false);
        else shopPanel.SetActive(false);
        pausePanel.SetActive(true);

        var p  = FindObjectOfType<PlayerController>();
        var bm = FindObjectOfType<BoidManager>();

        // 金鱼概率（保持你原有写法，避免缺字段编译失败）
        float goldChance = bm ? Mathf.Clamp01(bm.goldenChance + bm.goldenChanceAddFromSpeed) : 0f;

        // 新：每波鱼群的小鱼数量（受所有道具与加成影响）
        int smallPerWave = bm ? bm.boidsPerWave : 0;


        // 当前暴击倍率
        float curCritMult = critMultiplier;
        float critRate    = CritChance;

        if (p && pauseStatsText)
            pauseStatsText.text =
                $"{p.CurrentMaxSpeed:F1}\n" +   // 速度
                $"{p.CurrentAccelRate:F1}\n" +  // 加速度
                $"{goldChance:P1}\n" +          // 金鱼概率
                $"{critRate:P1}\n" +            // 暴击率
                $"{smallPerWave}\n" +           // ★ 鱼群中的小鱼数量（每波）
                $"{curCritMult}";           // 暴击倍率

    }


    /* ========== HUD / 工具 ========== */
    void RefreshHUD()
    {
        if (timerText) timerText.text = $"{timeLeft:F1}s";
        if (scoreText) scoreText.text = $"{currency}";
        if (hudTargetText) hudTargetText.text = $"{targetScore}";
    }

    void UpdateContinueButton()
    {
        if (!mainMenuContinueButton) return;
        mainMenuContinueButton.interactable = hasSuspended;
    }

    void ClearScene()
    {
        var bm = FindObjectOfType<BoidManager>(); if (bm) bm.ClearAllBoids();
        var sm = FindObjectOfType<SpikeManager>(); if (sm) sm.ClearAll();
    }

    void ResetPlayerToSpawn()
    {
#if UNITY_2023_1_OR_NEWER
        var p = FindAnyObjectByType<PlayerController>();
#else
        var p = FindObjectOfType<PlayerController>();
#endif
        if (!p) return;
        p.ResetAt(playerSpawn);
    }

    void CaptureBaselines()
    {
        if (baselineCaptured) return;
        var p = FindObjectOfType<PlayerController>();
        var bm = FindObjectOfType<BoidManager>();
        if (p)
        {
            basePlayerMaxSpeed = p.maxSpeed;
            basePlayerAccelRate = p.accelRate;
            basePlayerScale = p.transform.localScale;
        }
        if (bm)
        {
            baseBoidsPerWave = bm.boidsPerWave;
            baseGoldenChance = bm.goldenChance;
        }
        baselineCaptured = true;
    }

    void ResetRunUpgrades()
    {
        // 1) 撤销运行期订阅与累计
        GlobalEvents.ResetAllListeners();
        WireCritListener();
        CritResetModel();

        // 2) 恢复管理器参数
        var bm = FindObjectOfType<BoidManager>();
        if (bm)
        {
            bm.boidsPerWave = baseBoidsPerWave;
            bm.goldenChance = baseGoldenChance;
        }

        // 3) 恢复玩家数值与外观
        var p = FindObjectOfType<PlayerController>();
        if (p)
        {
            p.maxSpeed = basePlayerMaxSpeed;
            p.accelRate = basePlayerAccelRate;
            p.transform.localScale = basePlayerScale;
        }

    }
    public void OnAutoPlayToggle(bool on)
    {
        UnityEngine.PlayerPrefs.SetInt(PP_AUTO, on ? 1 : 0);
        UnityEngine.PlayerPrefs.Save();
    }   

    void ApplyAutoPlayState()
    {
        var pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (!pc) return;

        bool auto = (autoPlayToggle && autoPlayToggle.isOn);   // 仅看UI实时勾选

        // 先复位输入通道
        pc.externalMoveDir = Vector2.zero;
        pc.externalSprint  = false;
        pc.EnableExternalControl(auto);

        var ai = pc.GetComponent<AutopilotPredator>();
        if (auto)
        {
            if (!ai) ai = pc.gameObject.AddComponent<AutopilotPredator>();
            ai.enabled = true;
        }
        else
        {
            if (ai) { ai.enabled = false; Destroy(ai); }   // 直接移除，杜绝 OnEnable 抢控制
        }
    }


    #if UNITY_EDITOR
    void UpdateFishCounters()
    {
        int white = 0, gold = 0;

        // 编辑器下直接扫描场景中的 Boid 组件，避免依赖 ActiveBoids
        var boids = FindObjectsByType<Boid>(FindObjectsSortMode.None);
        for (int i = 0; i < boids.Length; i++)
        {
            if (!boids[i]) continue;
            if (boids[i].isGolden) gold++; else white++;
        }

        if (whiteCountText) whiteCountText.text = $"{white}";
        if (goldCountText)  goldCountText.text  = $"{gold}";
    }
    #endif
    void WireCritListener()
    {
        GlobalEvents.OnFishEaten -= OnFishEatenCore;   // 防重复
        GlobalEvents.OnFishEaten += OnFishEatenCore;
    }
    void OnFishEatenCore(Boid boid, Vector2 pos)
    {
        if (!boid) return;

        int baseScore = boid.scoreValue;
        // 叠加型加分
        int add = boid.isGolden ? goldScoreAdd : smallScoreAdd;
        // 若你仍保留“分数下限 floor”，与加分并行取最大
        int floor = boid.isGolden ? goldScoreFloor : smallScoreFloor;  // 若没有 floor，可将其视为 0
        int effBase = Mathf.Max(baseScore + add, floor);

        int baseDelta = effBase - baseScore;
        if (baseDelta > 0) AddScore(baseDelta);

        // ……下面保持你现有的暴击判定与加分（基于 effBase 计算额外分）……
        float chance = CritChance;
        if (chance > 0f && UnityEngine.Random.value < chance)
        {
            int extra = Mathf.RoundToInt(effBase * (critMultiplier - 1f));
            if (extra > 0) AddScore(extra);
            OnCrit?.Invoke();
        }
    }



}
