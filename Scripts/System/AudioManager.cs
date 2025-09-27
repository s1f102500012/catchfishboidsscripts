using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }

    [Header("Clips")]
    public AudioClip bgm;
    public AudioClip eat;
    public AudioClip eatGold;
    public AudioClip buy;

    AudioSource music;
    AudioSource sfx;

    void Awake()
    {
        if (I) { Destroy(gameObject); return; }
        I = this; DontDestroyOnLoad(gameObject);

        music = gameObject.AddComponent<AudioSource>();
        sfx   = gameObject.AddComponent<AudioSource>();
        music.loop = true;           // 背景音乐循环
        music.clip = bgm;
        music.Play();
    }

    /* ------- 公共快捷接口 ------- */
    public void PlayEat(bool golden = false) => sfx.PlayOneShot(golden ? eatGold : eat);
    public void PlayBuy()                    => sfx.PlayOneShot(buy);
}
