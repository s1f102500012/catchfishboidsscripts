using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class Trail2D : MonoBehaviour
{
    [Header("Tune")]
    public float trailTime = 0.35f;          // 拖尾存在时间
    public float width     = 0.30f;          // 头部宽度（尾部会渐细到 0）
    public float minVertexDistance = 0.08f;  // 采样间距（越小越丝滑，成本越高）
    public int   capVertices = 6;            // 圆头圆尾
    public bool  useSpriteColor = true;      // 自动读取 Sprite 颜色
    public Color baseColor = new(1,0.6f,0,1);// 备用颜色（无 SpriteRenderer 时使用）
    [Range(0,1)] public float headAlpha = 0.85f;
    [Range(0,1)] public float tailAlpha = 0.0f;
    public int   sortingOrderOffset = -1;    // 比主体低 1，绘制在身后

    [Header("Runtime")]
    public TrailRenderer tr;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Color lastColor;

    void Awake()
    {
        if (!tr) tr = GetComponent<TrailRenderer>();
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        // 基础参数
        tr.time = trailTime;
        tr.minVertexDistance = minVertexDistance;
        tr.widthMultiplier = width;
        tr.alignment = LineAlignment.View;
        tr.numCapVertices = capVertices;
        tr.emitting = true;              // 运行时按速度自动开关
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        // 宽度从 1 到 0
        var w = new AnimationCurve();
        w.AddKey(0f, 1f);
        w.AddKey(1f, 0f);
        tr.widthCurve = w;

        RefreshFromSprite();             // 颜色与排序
        tr.Clear();
    }

    void OnEnable()  { tr.Clear(); }
    void OnDisable() { tr.Clear(); }

    void LateUpdate()
    {
        // 低速时不发拖尾（降成本）
        if (rb)
            tr.emitting = rb.linearVelocity.sqrMagnitude > 0.01f;

        // 运行时颜色被改（例如金鱼）→ 实时刷新渐变与排序
        if (useSpriteColor && sr && sr.color != lastColor)
            RefreshFromSprite();
    }

    public void RefreshFromSprite()
    {
        Color c = useSpriteColor && sr ? sr.color : baseColor;
        lastColor = c;

        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(c, 0f),
                new GradientColorKey(c, 1f)
            },
            new[] {
                new GradientAlphaKey(headAlpha, 0f),
                new GradientAlphaKey(tailAlpha, 1f)
            });
        tr.colorGradient = grad;

        // 跟随 Sprite 的 Sorting，确保拖尾在身后
        if (sr)
        {
#if UNITY_2021_2_OR_NEWER
            tr.sortingLayerID = sr.sortingLayerID;
            tr.sortingOrder   = sr.sortingOrder + sortingOrderOffset;
#endif
        }
    }

    // 外部复位（重生/传送/回主菜单时调用可避免“穿屏线”）
    public void ClearTrail() => tr.Clear();
}
