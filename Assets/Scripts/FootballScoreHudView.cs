using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballScoreHudView : MonoBehaviour
{
    [SerializeField] private TMP_Text _leftScoreText;
    [SerializeField] private TMP_Text _rightScoreText;

    private string _currentLeftScoreText;
    private string _currentRightScoreText;

    public void ShowScore(int leftScore, int rightScore)
    {
        SetText(_leftScoreText, ref _currentLeftScoreText, leftScore.ToString());
        SetText(_rightScoreText, ref _currentRightScoreText, rightScore.ToString());
    }

    private static void SetText(TMP_Text text, ref string currentValue, string value)
    {
        if (text == null || currentValue == value)
            return;

        text.text = value;
        currentValue = value;
    }
}
