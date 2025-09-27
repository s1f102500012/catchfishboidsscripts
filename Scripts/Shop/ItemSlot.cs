using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemSlot : MonoBehaviour
{
    public Image   background;
    public TMP_Text nameText;
    public Image   icon;
    public TMP_Text descText;
    public TMP_Text priceText;

    [HideInInspector] public ItemConfig cfg;

    Button btn; ShopManager mgr;
    bool sold = false;
    int  lockedPrice = 0;                 // 本页固定价

    public bool Sold => sold;

    static readonly Color32[] RARITY_COLORS = {
        new Color32(200,200,200,255),
        new Color32( 70,130,255,255),
        new Color32(180, 90,255,255),
        new Color32(255,165, 50,255)
    };

    void Awake(){ btn = GetComponent<Button>(); }

    // 常规初始化：按“当前应价”锁定本页价格
    public void Init(ItemConfig c, ShopManager m)
    {
        cfg = c; mgr = m; sold = false;

        if (background) background.color = RARITY_COLORS[(int)c.rarity];
        if (nameText)   nameText.text = c.LocalizedName;
        if (icon)       icon.sprite   = c.icon;
        if (descText)   descText.text = c.LocalizedDesc;

        lockedPrice = m.GetCurrentPrice(cfg);
        if (priceText) priceText.text = lockedPrice.ToString();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(()=>mgr.TryBuy(this));
        btn.interactable = true;
    }

    // 锁定初始化：使用“携带过来的原价”
    public void InitLocked(ItemConfig c, ShopManager m, int price)
    {
        cfg = c; mgr = m; sold = false;

        if (background) background.color = RARITY_COLORS[(int)c.rarity];
        if (nameText)   nameText.text = c.LocalizedName;
        if (icon)       icon.sprite   = c.icon;
        if (descText)   descText.text = c.LocalizedDesc;

        lockedPrice = Mathf.Max(0, price);
        if (priceText) priceText.text = lockedPrice.ToString();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(()=>mgr.TryBuy(this));
        btn.interactable = true;
    }

    public int GetLockedPrice() => lockedPrice;

    public void MarkSold()
    {
        sold = true;
        if (priceText) priceText.text = "SOLD";
        if (btn) btn.interactable = false;
    }
}
