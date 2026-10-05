using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class FootballLocalizedText : MonoBehaviour
{
    [SerializeField] private string _key;
    private TMP_Text _label;
    private string _source;
    private object[] _arguments;

    public void SetSource(string source, params object[] arguments)
    {
        _key = "";
        _source = source;
        _arguments = arguments;
        Refresh();
    }

    public void SetKey(string key)
    {
        _key = key;
        _source = null;
        _arguments = null;
        Refresh();
    }

    private void OnEnable()
    {
        FootballLocalization.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable() => FootballLocalization.LanguageChanged -= Refresh;

    private void Refresh()
    {
        if (_label == null)
            _label = GetComponent<TMP_Text>();
        if (!string.IsNullOrEmpty(_key))
            _label.text = FootballLocalization.Get(_key);
        else if (_source != null)
            _label.text = FootballLocalization.Format(_source, _arguments);
    }
}
