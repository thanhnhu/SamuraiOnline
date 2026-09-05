using UnityEngine;
using UnityEngine.UI;

public class SimpleUIManager : MonoBehaviour
{
    public CharacterStatus player;
    public CharacterStatus enemy;
    public Slider playerHealthBar;
    public Slider enemyHealthBar;
    public Text playerStateText;
    public Text enemyStateText;
    public Text roundResultText;

    void Start()
    {
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (playerHealthBar) playerHealthBar.value = (float)player.currentHealth / player.maxHealth;
        if (enemyHealthBar) enemyHealthBar.value = (float)enemy.currentHealth / enemy.maxHealth;
        if (playerStateText) playerStateText.text = player.state.ToString();
        if (enemyStateText) enemyStateText.text = enemy.state.ToString();
    }

    public void ShowRoundResult(string message)
    {
        if (roundResultText)
        {
            roundResultText.text = message;
            Invoke(nameof(ClearRoundResult), 1.5f);
        }
    }

    void ClearRoundResult()
    {
        if (roundResultText) roundResultText.text = "";
    }
} 