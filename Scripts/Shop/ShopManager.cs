using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [Header("UI")]
    public ItemSlot[] slots;
    public TMP_Text   availText;
    public Button     nextRoundBtn;

    [Header("Pool")]
    public ItemConfig[] pool;

    // ---------- Reroll & Lock ----------
    [Header("Reroll & Lock")]
    public Button   rerollBtn;
    public TMP_Text rerollCostText;
    public Toggle   lockToggle;

    // ---------- 本大局禁用（唯一性道具） ----------
    static readonly HashSet<ItemConfig> bannedThisRun = new();
    public static void BanThisRun(ItemConfig cfg){ if (cfg) bannedThisRun.Add(cfg); }
    public static void ResetRunBans(){ bannedThisRun.Clear(); }

    // ---------- 重随费用（本大局） ----------
    // 规则：初始20；每次使用×2；每到商店（每过一小局）×0.75
    static int  rerollBase = 20;
    static int  rerollCost = 20;
    public static void ResetRerollCost(){ rerollCost = rerollBase; }

    // ---------- 锁定快照（锁定开着就一直沿用） ----------
    struct SlotSnapshot { public ItemConfig cfg; public int price; public bool sold; }
    static readonly List<SlotSnapshot> carryPage = new();
    static bool carryValid = false;

    // ---------- 乘法涨价状态（本大局） ----------
    // 现价 = 基础价 × 全局乘子 × 稀有度乘子 × 商品乘子
    static float allMul = 1f;
    static readonly Dictionary<ItemRarity, float> rarityMul = new();   // 初始1
    static readonly Dictionary<ItemConfig, float> itemMul   = new();   // 初始1
    const float ALL_ITEMS_FACTOR   = 1.05f;   // 购买后：所有商品 ×1.05
    const float SAME_RARITY_FACTOR = 1.10f;   // 同稀有度 ×1.10
    const float SAME_ITEM_FACTOR   = 1.20f;   // 同商品   ×1.20

    public static void ResetPriceScaling()
    {
        allMul = 1f;
        rarityMul.Clear();
        itemMul.Clear();
    }

    void OnEnable()
    {
        RefreshHeader();
        RefreshRerollUI();
    }

    // ====================== UI hooks ======================
    public void OnClickReroll()
    {
        if (lockToggle && lockToggle.isOn) return;   // 锁定时不可重随
        var gm = GameManager.Instance; if (!gm) return;

        if (gm.CurrentMoney < rerollCost) return;
        if (!gm.Spend(rerollCost)) return;

        rerollCost = Mathf.Max(rerollBase, rerollCost * 2);
        RollOffers();
        RefreshHeader();
        RefreshRerollUI();
    }

    public void OnToggleLock(bool on)
    {
        if (on) SnapshotLockedOffers();
        else    ClearCarry();
        RefreshRerollUI();
    }

    // ====================== 开店 ======================
    public void OpenShop()
    {
        // 每到一次商店 → 按规则折扣 0.75
        rerollCost = Mathf.Max(rerollBase, Mathf.CeilToInt(rerollCost * 0.75f));

        if (lockToggle && lockToggle.isOn && carryValid && carryPage.Count > 0)
        {
            int i = 0;
            for (; i < slots.Length && i < carryPage.Count; i++)
            {
                var s = carryPage[i];
                slots[i].InitLocked(s.cfg, this, s.price);
                if (s.sold) slots[i].MarkSold();
            }
            if (i < slots.Length) RollOffers(startIndex: i);
        }
        else
        {
            RollOffers();
        }

        RefreshHeader();
        RefreshRerollUI();
    }

    void RollOffers(int startIndex = 0)
    {
        if (pool == null || pool.Length == 0) return;
        var used = new HashSet<ItemConfig>();

        for (int i = startIndex; i < slots.Length; i++)
        {
            ItemConfig pick = pool[Random.Range(0, pool.Length)];
            int guard = 0;
            while ((used.Contains(pick) || bannedThisRun.Contains(pick)) && guard++ < 50 && used.Count < pool.Length)
                pick = pool[Random.Range(0, pool.Length)];
            used.Add(pick);

            slots[i].Init(pick, this);   // 锁定“本页固定价”
        }
    }

    // ====================== 价格计算（乘法） ======================
    public int GetCurrentPrice(ItemConfig cfg)
    {
        float rm = rarityMul.TryGetValue(cfg.rarity, out var r) ? r : 1f;
        float im = itemMul.TryGetValue(cfg, out var i) ? i : 1f;
        float priceF = cfg.price * allMul * rm * im;
        // 向上取整，至少1
        return Mathf.Max(rerollBase, Mathf.CeilToInt(priceF));
    }

    void BumpPrices(ItemConfig cfg)
    {
        // 购买后：全体×1.05；同商品×1.20；同稀有度×1.10
        allMul *= ALL_ITEMS_FACTOR;

        itemMul[cfg] = (itemMul.TryGetValue(cfg, out var im) ? im : 1f) * SAME_ITEM_FACTOR;
        rarityMul[cfg.rarity] = (rarityMul.TryGetValue(cfg.rarity, out var rm) ? rm : 1f) * SAME_RARITY_FACTOR;
    }

    // ====================== 购买 ======================
    public void TryBuy(ItemSlot slot)
    {
        if (!slot || slot.cfg == null) return;

        int price = slot.GetLockedPrice();         // 本页固定价
        var gm = GameManager.Instance; if (!gm) return;
        if (gm.CurrentMoney < price) return;
        if (!gm.Spend(price)) return;

        if (slot.cfg.modifier)
        {
            var player = FindFirstObjectByType<PlayerController>();
            slot.cfg.modifier.Apply(player);
        }

        if (AudioManager.I != null) AudioManager.I.PlayBuy();

        slot.MarkSold();

        // 购买后更新乘子（对“下一页/下一次商店/重随后”生效；本页不改价）
        BumpPrices(slot.cfg);

        // 本大局唯一性道具：买后禁用
        if (slot.cfg.uniquePerRun) BanThisRun(slot.cfg);

        RefreshHeader();

        // 若锁定开启，更新快照（保持 SOLD 和原价）
        if (lockToggle && lockToggle.isOn) SnapshotLockedOffers();
    }

    // ====================== 小工具 ======================
    void SnapshotLockedOffers()
    {
        carryPage.Clear();
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s == null || s.cfg == null) continue;
            carryPage.Add(new SlotSnapshot{ cfg = s.cfg, price = s.GetLockedPrice(), sold = s.Sold });
        }
        carryValid = carryPage.Count > 0;
    }

    void ClearCarry(){ carryPage.Clear(); carryValid = false; }

    void RefreshHeader()
    {
        var gm = GameManager.Instance;
        if (gm && availText) availText.text = gm.CurrentMoney.ToString();
    }

    void RefreshRerollUI()
    {
        if (rerollCostText) rerollCostText.text = rerollCost.ToString();
        if (rerollBtn) rerollBtn.interactable = !(lockToggle && lockToggle.isOn);
    }

    // 供“新一大局”时调用
    public static void HardResetForNewRun()
    {
        ResetPriceScaling();
        ResetRunBans();
        ResetRerollCost();
        // 锁定快照是否清除由你的流程控制；通常回主菜单会关掉锁定。
    }

    public bool IsPageEmpty()
    {
        if (slots == null || slots.Length == 0) return true;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].cfg != null) return false; // 有任意有效卡片
        return true; // 全是占位
    }
}
