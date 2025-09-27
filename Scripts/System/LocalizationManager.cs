using UnityEngine;
using System;

public enum Lang { EN = 0, ZH = 1, JA = 2 }

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager I { get; private set; }
    public static event Action OnLanguageChanged;

    Lang current = Lang.EN;

    public Lang Current => current;

    void Awake()
    {
        if (I) { Destroy(gameObject); return; }
        I = this; DontDestroyOnLoad(gameObject);

    }

    void Start()
    {
        OnLanguageChanged?.Invoke();                // 保持：启动即刷新所有文本
    }


    public void SetLanguage(int index) => Set((Lang)index);
    public void Set(Lang lang)
    {
        if (current == lang) return;
        current = lang;
        OnLanguageChanged?.Invoke();
    }

    public string Pick(string en, string zh, string ja)
    {
        return current switch
        {
            Lang.ZH => string.IsNullOrEmpty(zh) ? en : zh,
            Lang.JA => string.IsNullOrEmpty(ja) ? en : ja,
            _       => en
        };
    }
}
