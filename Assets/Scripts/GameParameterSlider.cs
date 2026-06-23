using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameParameterSlider : MonoBehaviour
{
    private const string DefaultValueFormat = "0.0";

    [SerializeField] private GameParameterId _parameter = GameParameterId.BallGravity;
    [SerializeField] private Slider _slider;
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private string _valueFormat = DefaultValueFormat;

    public GameParameterId Parameter => _parameter;
    public string ParameterKey => GameParameterDefinitions.GetKey(_parameter);
    public float Value => _slider != null ? _slider.value : GameParameterSessionValues.GetValue(_parameter);

    private void Reset()
    {
        _slider = GetComponentInChildren<Slider>(true);
        _valueText = GetComponentInChildren<TMP_Text>(true);
    }

    private void Awake()
    {
        ApplyStoredValue();
    }

    private void OnEnable()
    {
        ApplyStoredValue();

        if (_slider != null)
            _slider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    private void OnDisable()
    {
        if (_slider != null)
            _slider.onValueChanged.RemoveListener(OnSliderValueChanged);
    }

    private void OnValidate()
    {
        RefreshText(_slider != null ? _slider.value : GameParameterDefinitions.GetDefaultValue(_parameter));
    }

    private void OnSliderValueChanged(float value)
    {
        GameParameterSessionValues.SetValue(_parameter, value);
        RefreshText(value);
    }

    private void ApplyStoredValue()
    {
        float value = GameParameterSessionValues.GetValue(_parameter);

        if (_slider != null)
        {
            _slider.SetValueWithoutNotify(value);
            value = _slider.value;
        }

        GameParameterSessionValues.SetValue(_parameter, value);
        RefreshText(value);
    }

    private void RefreshText(float value)
    {
        if (_valueText == null)
            return;

        _valueText.text = value.ToString(GetValueFormat(), CultureInfo.InvariantCulture);
    }

    private string GetValueFormat()
    {
        return string.IsNullOrEmpty(_valueFormat) ? DefaultValueFormat : _valueFormat;
    }
}
