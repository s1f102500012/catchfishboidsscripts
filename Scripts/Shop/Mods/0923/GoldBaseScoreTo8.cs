using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/+2 Gold Score")]
public class GoldBaseScoreTo8 : PlayerModifier
{
    public override void Apply(PlayerController player)
    {
        GameManager.AddGoldScoreBonus(2);   // 仅写入加成，结算在 GameManager
    }
}
