using UnityEngine;

public abstract class PlayerModifier : ScriptableObject
{
    public abstract void Apply(PlayerController player);     // 具体增益
}
