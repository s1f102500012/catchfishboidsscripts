using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 6f;
    public float accelRate = 20f;
    public float brakeRate = 15f;

    [Header("World Bounds")]
    public Vector2 halfBounds = new(16, 9);
    [Range(0f, 1f)] public float wallBounceDamping = 0.9f;

    // ---------- Sprint（Shift 提速，带每秒扣费） ----------
    [Header("Sprint")]
    public float sprintSpeedMult = 1.5f;  // 冲刺最大速度倍率
    public float sprintAccelMult = 0.5f;  // 冲刺加速度倍率
    public float sprintCostPerSec = 3f;    // 每秒扣的钱

    [Header("Auto/External Control")]
    public bool externalControl = false;              // 是否由AI接管
    [HideInInspector] public Vector2 externalMoveDir; // AI给的方向，单位向量
    [HideInInspector] public bool externalSprint;     // AI给的冲刺开关
    public void EnableExternalControl(bool on){ externalControl = on; }


    // 提供给 ESC 面板显示
    public float CurrentMaxSpeed => maxSpeed * (isSprintingEffective ? sprintSpeedMult : 1f);
    public float CurrentAccelRate =>
    accelRate * (isSprintingEffective
        ? (ShiftNoAccelPenalty.Enabled ? 1f : sprintAccelMult)   // 装备道具时冲刺不降加速度
        : 1f);


    // ---------- runtime ----------
    Rigidbody2D rb;
    SpriteRenderer sr;
    Vector2 moveInput;

    bool sprintHeld;                // Shift 是否按住
    bool isSprintingEffective;      // 本帧是否生效
    float sprintCostAcc = 0f;       // 累计费率（到 1 扣 1 点）

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        externalControl = false;
        externalMoveDir = Vector2.zero;
        externalSprint  = false;
    }

    // 新输入系统
    public void OnMove(InputValue v) => moveInput = v.Get<Vector2>();

    void Update()
    {
        var gm = GameManager.Instance;
        bool running = gm && gm.IsRunning;

        // 读取 Shift
        if (externalControl)
        {
            sprintHeld = externalSprint;
        }
        else if (Keyboard.current != null)
        {
            sprintHeld = Keyboard.current.leftShiftKey.isPressed
                      || Keyboard.current.rightShiftKey.isPressed;
        }
        else sprintHeld = false;

        if (!running)
        {
            isSprintingEffective = false;
            return;
        }

        // 余额>0 或有未结清的累计费用时才允许冲刺生效
        bool canAfford = (gm.CurrentMoney > 0) || (sprintCostAcc > 0f);
        isSprintingEffective = sprintHeld && canAfford;

        // 扣费：只在冲刺实际生效时累加；累计到 1 再扣整点
        if (isSprintingEffective)
        {
            sprintCostAcc += sprintCostPerSec * Time.deltaTime;

            while (sprintCostAcc >= 1f)
            {
                if (gm.Spend(1))
                {
                    sprintCostAcc -= 1f;
                }
                else
                {
                    // 余额不足 → 立即停止冲刺并清零累计
                    isSprintingEffective = false;
                    sprintCostAcc = 0f;
                    break;
                }
            }
        }
    }

    void FixedUpdate()
    {
        var gm = GameManager.Instance;

        if (!(gm && gm.IsRunning))
        {
#if UNITY_2023_1_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.velocity = Vector2.zero;
#endif
            return;
        }

        float effMaxSpeed = CurrentMaxSpeed;
        float effAccelRate = CurrentAccelRate;
        
        Vector2 inputDir = externalControl ? externalMoveDir : moveInput;
        Vector2 accel = inputDir.normalized * effAccelRate;

#if UNITY_2023_1_OR_NEWER
        Vector2 v = rb.linearVelocity + accel * Time.fixedDeltaTime;
#else
        Vector2 v = rb.velocity + accel * Time.fixedDeltaTime;
#endif

        // 松开方向键时刹车
        if (inputDir.sqrMagnitude < 0.01f)
            v = Vector2.MoveTowards(v, Vector2.zero, brakeRate * Time.fixedDeltaTime);

#if UNITY_2023_1_OR_NEWER
        rb.linearVelocity = Vector2.ClampMagnitude(v, effMaxSpeed);
        if (rb.linearVelocity.sqrMagnitude > 0.01f) transform.up = rb.linearVelocity;
#else
        rb.velocity = Vector2.ClampMagnitude(v, effMaxSpeed);
        if (rb.velocity.sqrMagnitude > 0.01f) transform.up = rb.velocity;
#endif

        BounceWalls();
    }

    // ---------- 吃鱼 ----------
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out Boid boid))
        {
            int times = boid.EatCount;                       // 2^tier
            for (int i = 0; i < times; i++)
            {
                GameManager.Instance.AddScore(boid.scoreValue);
                GlobalEvents.RaiseFishEaten(boid, boid.transform.position);
            }

            // ★ 循环外只播一次
            PlayEatOnce(boid.isGolden);

            var mgr = BoidManager.Instance;
            if (mgr) mgr.DespawnBoid(boid); else Destroy(boid.gameObject);
        }
    }

    // ---------- 边界弹返 ----------
    void BounceWalls()
    {
#if UNITY_2023_1_OR_NEWER
        Vector2 pos = rb.position, vel = rb.linearVelocity;
#else
        Vector2 pos = rb.position, vel = rb.velocity;
#endif
        Vector2 half = halfBounds; bool hit = false;

        if (pos.x > half.x) { pos.x = half.x; vel.x = -vel.x * wallBounceDamping; hit = true; }
        if (pos.x < -half.x) { pos.x = -half.x; vel.x = -vel.x * wallBounceDamping; hit = true; }
        if (pos.y > half.y) { pos.y = half.y; vel.y = -vel.y * wallBounceDamping; hit = true; }
        if (pos.y < -half.y) { pos.y = -half.y; vel.y = -vel.y * wallBounceDamping; hit = true; }

        if (hit)
        {
            rb.position = pos;
#if UNITY_2023_1_OR_NEWER
            rb.linearVelocity = vel;
#else
            rb.velocity = vel;
#endif
            transform.position = pos;
        }
    }

    // ---------- 外部调用 ----------
    public void ResetAt(Vector2 pos)
    {
#if UNITY_2023_1_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.velocity = Vector2.zero;
#endif
        rb.angularVelocity = 0f;
        rb.position = pos;
        transform.position = pos;
        transform.up = Vector2.up;

        // 重置冲刺状态
        sprintHeld = false;
        isSprintingEffective = false;
        sprintCostAcc = 0f;

        GetComponent<Trail2D>()?.ClearTrail();
    }

    public void Knockback(Vector2 dir, float force)
    {
#if UNITY_2023_1_OR_NEWER
        rb.linearVelocity = dir.normalized * force;
#else
        rb.velocity = dir.normalized * force;
#endif
        transform.up = dir;
    }
    
    static float sNextEatSfxTime = 0f;
    static void PlayEatOnce(bool isGolden)
    {
        if (AudioManager.I == null) return;
        if (Time.unscaledTime < sNextEatSfxTime) return; // 50ms 防抖
        AudioManager.I.PlayEat(isGolden);
        sNextEatSfxTime = Time.unscaledTime + 0.05f;
    }
}
