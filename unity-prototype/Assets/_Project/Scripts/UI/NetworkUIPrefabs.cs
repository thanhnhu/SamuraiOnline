using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class NetworkUIPrefabs
{
    [Header("Connection UI")]
    public GameObject connectionPanelPrefab;
    public GameObject connectionStatusPrefab;
    public GameObject connectButtonPrefab;
    public GameObject disconnectButtonPrefab;
    public GameObject connectionProgressPrefab;
    
    [Header("Matchmaking UI")]
    public GameObject matchmakingPanelPrefab;
    public GameObject findMatchButtonPrefab;
    public GameObject createPrivateMatchButtonPrefab;
    public GameObject joinPrivateMatchButtonPrefab;
    public GameObject stopMatchmakingButtonPrefab;
    public GameObject searchStatusPrefab;
    public GameObject searchProgressPrefab;
    public GameObject searchTimePrefab;
    
    [Header("Room UI")]
    public GameObject roomPanelPrefab;
    public GameObject roomNameTextPrefab;
    public GameObject playerCountTextPrefab;
    public GameObject roomStatusTextPrefab;
    public GameObject leaveRoomButtonPrefab;
    public GameObject startGameButtonPrefab;
    public GameObject readyButtonPrefab;
    public GameObject playerListPrefab;
    public GameObject playerListItemPrefab;
    
    [Header("Player Settings UI")]
    public GameObject settingsPanelPrefab;
    public GameObject characterDropdownPrefab;
    public GameObject skillLevelDropdownPrefab;
    public GameObject regionDropdownPrefab;
    public GameObject crossRegionTogglePrefab;
    public GameObject rankedMatchTogglePrefab;
    
    [Header("Private Match UI")]
    public GameObject privateMatchPanelPrefab;
    public GameObject roomNameInputPrefab;
    public GameObject passwordInputPrefab;
    public GameObject createRoomButtonPrefab;
    public GameObject joinRoomButtonPrefab;
    
    [Header("Network Info UI")]
    public GameObject networkInfoPanelPrefab;
    public GameObject pingTextPrefab;
    public GameObject latencyTextPrefab;
    public GameObject packetLossTextPrefab;
    public GameObject frameRateTextPrefab;
    
    [Header("Spectator UI")]
    public GameObject spectatorPanelPrefab;
    public GameObject spectatorListPrefab;
    public GameObject spectatorListItemPrefab;
    public GameObject spectateButtonPrefab;
    public GameObject leaveSpectatorButtonPrefab;
    public GameObject spectatorCountTextPrefab;
    
    [Header("Replay UI")]
    public GameObject replayPanelPrefab;
    public GameObject replayListPrefab;
    public GameObject replayListItemPrefab;
    public GameObject playReplayButtonPrefab;
    public GameObject pauseReplayButtonPrefab;
    public GameObject stopReplayButtonPrefab;
    public GameObject replayProgressPrefab;
    public GameObject replayTimeTextPrefab;
    public GameObject replaySpeedPrefab;
    public GameObject saveReplayButtonPrefab;
    public GameObject loadReplayButtonPrefab;
    
    [Header("Debug UI")]
    public GameObject debugPanelPrefab;
    public GameObject debugTextPrefab;
    public GameObject networkDebugTogglePrefab;
    public GameObject inputDebugTogglePrefab;
    public GameObject rollbackDebugTogglePrefab;
    
    [Header("Notification UI")]
    public GameObject notificationPanelPrefab;
    public GameObject notificationTextPrefab;
    public GameObject notificationButtonPrefab;
    public GameObject errorPanelPrefab;
    public GameObject errorTextPrefab;
    public GameObject errorButtonPrefab;
    
    [Header("Loading UI")]
    public GameObject loadingPanelPrefab;
    public GameObject loadingTextPrefab;
    public GameObject loadingProgressPrefab;
    public GameObject loadingSpinnerPrefab;
    
    [Header("Common UI Elements")]
    public GameObject buttonPrefab;
    public GameObject textPrefab;
    public GameObject inputFieldPrefab;
    public GameObject dropdownPrefab;
    public GameObject togglePrefab;
    public GameObject sliderPrefab;
    public GameObject panelPrefab;
    public GameObject scrollViewPrefab;
    public GameObject listItemPrefab;
}

public static class UIPrefabFactory
{
    public static GameObject CreateButton(string text, System.Action onClick = null)
    {
        GameObject buttonObj = new GameObject("Button");
        Button button = buttonObj.AddComponent<Button>();
        Image image = buttonObj.AddComponent<Image>();
        
        // Create text child
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform);
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.fontSize = 14;
        
        // Set up RectTransform
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Add click listener
        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }
        
        return buttonObj;
    }
    
    public static GameObject CreateText(string text, int fontSize = 14)
    {
        GameObject textObj = new GameObject("Text");
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = TextAlignmentOptions.Left;
        
        return textObj;
    }
    
    public static GameObject CreateInputField(string placeholder = "")
    {
        GameObject inputObj = new GameObject("InputField");
        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        Image image = inputObj.AddComponent<Image>();
        
        // Create placeholder
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputObj.transform);
        TextMeshProUGUI placeholderText = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholderText.text = placeholder;
        placeholderText.fontSize = 14;
        placeholderText.color = Color.gray;
        
        // Create text area
        GameObject textAreaObj = new GameObject("TextArea");
        textAreaObj.transform.SetParent(inputObj.transform);
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(textAreaObj.transform);
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.fontSize = 14;
        
        // Set up RectTransforms
        RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
        RectTransform textAreaRect = textAreaObj.GetComponent<RectTransform>();
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(10, 0);
        placeholderRect.offsetMax = new Vector2(-10, 0);
        
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = Vector2.zero;
        textAreaRect.offsetMax = Vector2.zero;
        
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 0);
        textRect.offsetMax = new Vector2(-10, 0);
        
        // Set up input field
        inputField.placeholder = placeholderText;
        inputField.textComponent = textComponent;
        inputField.textViewport = textAreaRect;
        
        return inputObj;
    }
    
    public static GameObject CreateDropdown(string[] options)
    {
        GameObject dropdownObj = new GameObject("Dropdown");
        TMP_Dropdown dropdown = dropdownObj.AddComponent<TMP_Dropdown>();
        Image image = dropdownObj.AddComponent<Image>();
        
        // Create label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(dropdownObj.transform);
        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = options.Length > 0 ? options[0] : "";
        labelText.fontSize = 14;
        
        // Create template
        GameObject templateObj = new GameObject("Template");
        templateObj.transform.SetParent(dropdownObj.transform);
        templateObj.SetActive(false);
        
        // Set up RectTransforms
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10, 0);
        labelRect.offsetMax = new Vector2(-30, 0);
        
        // Set up dropdown
        dropdown.options.Clear();
        foreach (string option in options)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        }
        dropdown.captionText = labelText;
        dropdown.template = templateObj.GetComponent<RectTransform>();
        
        return dropdownObj;
    }
    
    public static GameObject CreateToggle(string text, bool isOn = false)
    {
        GameObject toggleObj = new GameObject("Toggle");
        Toggle toggle = toggleObj.AddComponent<Toggle>();
        Image image = toggleObj.AddComponent<Image>();
        
        // Create background
        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(toggleObj.transform);
        Image backgroundImage = backgroundObj.AddComponent<Image>();
        
        // Create checkmark
        GameObject checkmarkObj = new GameObject("Checkmark");
        checkmarkObj.transform.SetParent(backgroundObj.transform);
        Image checkmarkImage = checkmarkObj.AddComponent<Image>();
        
        // Create label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(toggleObj.transform);
        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = text;
        labelText.fontSize = 14;
        
        // Set up RectTransforms
        RectTransform backgroundRect = backgroundObj.GetComponent<RectTransform>();
        RectTransform checkmarkRect = checkmarkObj.GetComponent<RectTransform>();
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        
        backgroundRect.anchorMin = new Vector2(0, 0.5f);
        backgroundRect.anchorMax = new Vector2(0, 0.5f);
        backgroundRect.sizeDelta = new Vector2(20, 20);
        backgroundRect.anchoredPosition = new Vector2(10, 0);
        
        checkmarkRect.anchorMin = Vector2.zero;
        checkmarkRect.anchorMax = Vector2.one;
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;
        
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.offsetMin = new Vector2(40, 0);
        labelRect.offsetMax = new Vector2(0, 0);
        
        // Set up toggle
        toggle.targetGraphic = image;
        toggle.graphic = checkmarkImage;
        toggle.isOn = isOn;
        
        return toggleObj;
    }
    
    public static GameObject CreateSlider(float minValue = 0f, float maxValue = 1f, float value = 0.5f)
    {
        GameObject sliderObj = new GameObject("Slider");
        Slider slider = sliderObj.AddComponent<Slider>();
        Image image = sliderObj.AddComponent<Image>();
        
        // Create background
        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(sliderObj.transform);
        Image backgroundImage = backgroundObj.AddComponent<Image>();
        
        // Create fill area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform);
        RectTransform fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
        
        // Create fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform);
        Image fillImage = fillObj.AddComponent<Image>();
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        
        // Create handle
        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(sliderObj.transform);
        Image handleImage = handleObj.AddComponent<Image>();
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        
        // Set up RectTransforms
        RectTransform backgroundRect = backgroundObj.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;
        
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(20, 20);
        
        // Set up slider
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = value;
        slider.targetGraphic = image;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        
        return sliderObj;
    }
    
    public static GameObject CreatePanel()
    {
        GameObject panelObj = new GameObject("Panel");
        Image image = panelObj.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 0.8f);
        
        return panelObj;
    }
    
    public static GameObject CreateScrollView()
    {
        GameObject scrollViewObj = new GameObject("ScrollView");
        ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        Image image = scrollViewObj.AddComponent<Image>();
        
        // Create viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform);
        Image viewportImage = viewportObj.AddComponent<Image>();
        Mask mask = viewportObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        
        // Create content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        
        // Set up RectTransforms
        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        
        // Set up scroll rect
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        
        return scrollViewObj;
    }
} 