using System;
using UnityEngine;

public static class GlobalEvents
{
    public static event Action<Boid, Vector2> OnFishEaten;
    public static event Action<Vector2> OnGoldFishEaten;
    public static event Action<Vector2> OnSmallFishEaten;

    public static void RaiseFishEaten(Boid boid, Vector2 pos)
    {
        OnFishEaten?.Invoke(boid, pos);
        if (boid != null)
        {
            if (boid.isGolden) OnGoldFishEaten?.Invoke(pos);
            else OnSmallFishEaten?.Invoke(pos);
        }
    }
    public static void ResetAllListeners()
    {
        OnFishEaten      = null;
        OnGoldFishEaten  = null;
        OnSmallFishEaten = null;
    }
    
    // ……你已有的事件……
    public static event Action<Boid, Vector2> OnFishBoundaryBounce;

    public static void RaiseFishBoundaryBounce(Boid b, Vector2 pos)
        => OnFishBoundaryBounce?.Invoke(b, pos);

}
