using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Menu Panels")]
    public GameObject mainPanel;
    public GameObject optionsPanel;
    public GameObject characterSelectPanel;
    public GameObject stageSelectPanel;

    [Header("Character Selection")]
    public Transform characterGrid;
    public GameObject characterButtonPrefab;
    public TextMeshProUGUI selectedCharacterText;

    [Header("Stage Selection")]
    public Transform stageGrid;
    public GameObject stageButtonPrefab;
    public TextMeshProUGUI selectedStageText;

    [Header("Options")]
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Toggle fullscreenToggle;
    public TMP_Dropdown resolutionDropdown;

    private void Start()
    {
        ShowMainPanel();
        InitializeOptions();
    }

    private void InitializeOptions()
    {
        // Initialize volume sliders
        musicVolumeSlider.value = AudioManager.Instance.musicVolume;
        sfxVolumeSlider.value = AudioManager.Instance.sfxVolume;

        // Initialize fullscreen toggle
        fullscreenToggle.isOn = Screen.fullScreen;

        // Initialize resolution dropdown
        resolutionDropdown.ClearOptions();
        var resolutions = Screen.resolutions;
        var options = new System.Collections.Generic.List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = $"{resolutions[i].width}x{resolutions[i].height}";
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
    }

    public void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        optionsPanel.SetActive(false);
        characterSelectPanel.SetActive(false);
        stageSelectPanel.SetActive(false);
    }

    public void ShowOptionsPanel()
    {
        mainPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    public void ShowCharacterSelectPanel()
    {
        mainPanel.SetActive(false);
        characterSelectPanel.SetActive(true);
    }

    public void ShowStageSelectPanel()
    {
        mainPanel.SetActive(false);
        stageSelectPanel.SetActive(true);
    }

    public void OnMusicVolumeChanged(float volume)
    {
        AudioManager.Instance.SetMusicVolume(volume);
    }

    public void OnSFXVolumeChanged(float volume)
    {
        AudioManager.Instance.SetSFXVolume(volume);
    }

    public void OnFullscreenToggled(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    public void OnResolutionChanged(int index)
    {
        Resolution resolution = Screen.resolutions[index];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    public void OnCharacterSelected(string characterName)
    {
        selectedCharacterText.text = $"Selected: {characterName}";
        // Store selected character in GameManager
        GameManager.Instance.SetSelectedCharacter(characterName);
    }

    public void OnStageSelected(string stageName)
    {
        selectedStageText.text = $"Selected: {stageName}";
        // Store selected stage in GameManager
        GameManager.Instance.SetSelectedStage(stageName);
    }

    public void OnStartGameClicked()
    {
        // Check if character and stage are selected
        if (string.IsNullOrEmpty(GameManager.Instance.SelectedCharacter) ||
            string.IsNullOrEmpty(GameManager.Instance.SelectedStage))
        {
            // Show error message
            return;
        }

        // Load battle scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("Battle");
    }

    public void OnQuitClicked()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
} 