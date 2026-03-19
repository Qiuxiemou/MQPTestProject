using UnityEngine;
public class ScoreProvider : MonoBehaviour
{
    public static ScoreProvider Instance;
    int currentScore;
    void Awake() { Instance = this; }
    public void SetScore(int score) { currentScore = score; }
    public int GetScore() { return currentScore; }
}