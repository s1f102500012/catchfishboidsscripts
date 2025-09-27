using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedTMP : MonoBehaviour
{
    [TextArea] public string en;
    [TextArea] public string zh;
    [TextArea] public string ja;

    TMP_Text t;
    bool registered;

    void Awake(){ t = GetComponent<TMP_Text>(); }

    void OnEnable(){
        LocalizationManager.OnLanguageChanged += Apply;
        Apply();    // 立刻按当前语言刷新一次
    }
    void OnDisable(){
        LocalizationManager.OnLanguageChanged -= Apply;
    }

    IEnumerator RegisterAndApply()
    {
        while (LocalizationManager.I == null) yield return null;
        if (!registered){
            LocalizationManager.OnLanguageChanged += Apply;
            registered = true;
        }
        Apply();
    }

    public void Apply()
    {
        if (!t) t = GetComponent<TMP_Text>();
        var lm = LocalizationManager.I;
        t.text = lm ? lm.Pick(en, zh, ja) : en;
    }
}
