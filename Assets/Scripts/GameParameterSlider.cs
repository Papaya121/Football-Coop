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

    [Header("Slider range")]
    [SerializeField] private bool _useCustomRange;
    [SerializeField] private float _minimumValue;
    [SerializeField] private float _maximumValue = 1f;

    public GameParameterId Parameter => _parameter;
    public string ParameterKey => GameParameterDefinitions.GetKey(_parameter);
    public float Value => _slider != null ? _slider.value : GameParameterSessionValues.GetValue(_parameter);

    private void Reset()
    {
        _slider = GetComponentInChildren<Slider>(true);
        _valueText = GetComponentInChildren<TMP_Text>(true);
        CopyParameterRange();
        ConfigureSliderRange();
    }

    private void Awake()
    {
        ConfigureSliderRange();
        ApplyStoredValue();
    }

    private void OnEnable()
    {
        ConfigureSliderRange();
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
        if (!_useCustomRange)
            CopyParameterRange();

        ConfigureSliderRange();

        if (_slider != null)
            _slider.SetValueWithoutNotify(GameParameterDefinitions.GetDefaultValue(_parameter));

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

    private void ConfigureSliderRange()
    {
        if (_slider == null)
            return;

        float minimum = _useCustomRange
            ? _minimumValue
            : GameParameterDefinitions.GetMinValue(_parameter);
        float maximum = _useCustomRange
            ? _maximumValue
            : GameParameterDefinitions.GetMaxValue(_parameter);

        if (maximum < minimum)
            maximum = minimum;

        _slider.minValue = minimum;
        _slider.maxValue = maximum;
    }

    private void CopyParameterRange()
    {
        _minimumValue = GameParameterDefinitions.GetMinValue(_parameter);
        _maximumValue = GameParameterDefinitions.GetMaxValue(_parameter);
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
