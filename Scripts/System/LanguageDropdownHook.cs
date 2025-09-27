using UnityEngine; using TMPro;
public class LanguageDropdownHook : MonoBehaviour
{
    public TMP_Dropdown dd;
    void Start(){
        if (!dd) dd = GetComponent<TMP_Dropdown>();
        dd.value = (int)(LocalizationManager.I ? LocalizationManager.I.Current : Lang.EN);
        dd.onValueChanged.AddListener(i => LocalizationManager.I.SetLanguage(i));
    }
}
