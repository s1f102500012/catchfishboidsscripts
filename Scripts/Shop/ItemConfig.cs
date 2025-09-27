using UnityEngine;

public enum ItemRarity { Common, Rare, Epic, Legendary }

[CreateAssetMenu(menuName = "CatchFish/Item")]
public class ItemConfig : ScriptableObject
{
    public string displayName;                 // 英文
    [TextArea] public string description;      // 英文
    public Sprite icon;
    public ItemRarity rarity;
    public int price;
    public PlayerModifier modifier;
    [Header("Flags")]
    public bool uniquePerRun = false;   // 购买后本大局禁用

    [Header("Localization (optional)")]
    public string displayNameZh;
    public string displayNameJa;
    [TextArea] public string descriptionZh;
    [TextArea] public string descriptionJa;

    public string LocalizedName =>
        LocalizationManager.I ? LocalizationManager.I.Pick(displayName, displayNameZh, displayNameJa) : displayName;

    public string LocalizedDesc =>
        LocalizationManager.I ? LocalizationManager.I.Pick(description, descriptionZh, descriptionJa) : description;
}
