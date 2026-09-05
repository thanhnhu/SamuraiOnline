using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UIComponents
{
    #region Panel Creation
    
    public static GameObject CreatePanel(string name, Transform parent = null)
    {
        GameObject panel = new GameObject(name);
        if (parent != null)
            panel.transform.SetParent(parent);
        
        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        Image image = panel.AddComponent<Image>();
        
        // Set default panel properties
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        image.color = new Color(0, 0, 0, 0.8f);
        
        return panel;
    }
    
    public static GameObject CreateScrollPanel(string name, Transform parent = null)
    {
        GameObject panel = CreatePanel(name, parent);
        
        ScrollRect scrollRect = panel.AddComponent<ScrollRect>();
        Mask mask = panel.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        
        // Create viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(panel.transform);
        
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        Image viewportImage = viewport.AddComponent<Image>();
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        
        // Create content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform);
        
        RectTransform contentRect = content.AddComponent<RectTransform>();
        VerticalLayoutGroup layoutGroup = content.AddComponent<VerticalLayoutGroup>();
        
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.spacing = 5f;
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);
        
        // Setup scroll rect
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        
        return panel;
    }
    
    #endregion
    
    #region Button Creation
    
    public static GameObject CreateButton(string text, System.Action onClick = null, Transform parent = null)
    {
        GameObject button = new GameObject("Button");
        if (parent != null)
            button.transform.SetParent(parent);
        
        RectTransform rectTransform = button.AddComponent<RectTransform>();
        Image image = button.AddComponent<Image>();
        Button buttonComponent = button.AddComponent<Button>();
        
        // Set button colors
        ColorBlock colors = buttonComponent.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        colors.selectedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        buttonComponent.colors = colors;
        
        // Create text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(button.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        textComponent.text = text;
        textComponent.fontSize = 14;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.color = Color.white;
        
        // Add click listener
        if (onClick != null)
        {
            buttonComponent.onClick.AddListener(() => onClick());
        }
        
        return button;
    }
    
    public static GameObject CreateIconButton(Sprite icon, System.Action onClick = null, Transform parent = null)
    {
        GameObject button = new GameObject("IconButton");
        if (parent != null)
            button.transform.SetParent(parent);
        
        RectTransform rectTransform = button.AddComponent<RectTransform>();
        Image image = button.AddComponent<Image>();
        Button buttonComponent = button.AddComponent<Button>();
        
        // Set button colors
        ColorBlock colors = buttonComponent.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        buttonComponent.colors = colors;
        
        // Set icon
        image.sprite = icon;
        image.color = Color.white;
        
        // Add click listener
        if (onClick != null)
        {
            buttonComponent.onClick.AddListener(() => onClick());
        }
        
        return button;
    }
    
    #endregion
    
    #region Text Creation
    
    public static GameObject CreateText(string text, int fontSize = 14, TextAlignmentOptions alignment = TextAlignmentOptions.Left, Transform parent = null)
    {
        GameObject textObj = new GameObject("Text");
        if (parent != null)
            textObj.transform.SetParent(parent);
        
        RectTransform rectTransform = textObj.AddComponent<RectTransform>();
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        
        return textObj;
    }
    
    public static GameObject CreateTitleText(string text, Transform parent = null)
    {
        GameObject titleObj = CreateText(text, 24, TextAlignmentOptions.Center, parent);
        titleObj.name = "TitleText";
        
        TextMeshProUGUI textComponent = titleObj.GetComponent<TextMeshProUGUI>();
        textComponent.fontStyle = FontStyles.Bold;
        textComponent.color = new Color(1f, 0.8f, 0.2f, 1f); // Gold color
        
        return titleObj;
    }
    
    public static GameObject CreateSubtitleText(string text, Transform parent = null)
    {
        GameObject subtitleObj = CreateText(text, 18, TextAlignmentOptions.Center, parent);
        subtitleObj.name = "SubtitleText";
        
        TextMeshProUGUI textComponent = subtitleObj.GetComponent<TextMeshProUGUI>();
        textComponent.color = new Color(0.8f, 0.8f, 0.8f, 1f); // Light gray
        
        return subtitleObj;
    }
    
    #endregion
    
    #region Input Field Creation
    
    public static GameObject CreateInputField(string placeholder = "", Transform parent = null)
    {
        GameObject inputObj = new GameObject("InputField");
        if (parent != null)
            inputObj.transform.SetParent(parent);
        
        RectTransform rectTransform = inputObj.AddComponent<RectTransform>();
        Image image = inputObj.AddComponent<Image>();
        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        
        // Set background
        image.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        
        // Create text area
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(inputObj.transform);
        
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10, 5);
        textAreaRect.offsetMax = new Vector2(-10, -5);
        
        // Create placeholder
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(textArea.transform);
        
        RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
        TextMeshProUGUI placeholderText = placeholderObj.AddComponent<TextMeshProUGUI>();
        
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        
        placeholderText.text = placeholder;
        placeholderText.fontSize = 14;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        placeholderText.fontStyle = FontStyles.Italic;
        
        // Create text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(textArea.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        textComponent.fontSize = 14;
        textComponent.color = Color.white;
        
        // Setup input field
        inputField.placeholder = placeholderText;
        inputField.textComponent = textComponent;
        inputField.textViewport = textAreaRect;
        
        return inputObj;
    }
    
    #endregion
    
    #region Slider Creation
    
    public static GameObject CreateSlider(float minValue = 0f, float maxValue = 1f, float value = 0.5f, Transform parent = null)
    {
        GameObject sliderObj = new GameObject("Slider");
        if (parent != null)
            sliderObj.transform.SetParent(parent);
        
        RectTransform rectTransform = sliderObj.AddComponent<RectTransform>();
        Slider slider = sliderObj.AddComponent<Slider>();
        
        // Create background
        GameObject background = new GameObject("Background");
        background.transform.SetParent(sliderObj.transform);
        
        RectTransform backgroundRect = background.AddComponent<RectTransform>();
        Image backgroundImage = background.AddComponent<Image>();
        
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        
        backgroundImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        
        // Create fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform);
        
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;
        
        // Create fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform);
        
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        Image fillImage = fill.AddComponent<Image>();
        
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        
        fillImage.color = new Color(0.2f, 0.6f, 1f, 1f);
        
        // Create handle
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(sliderObj.transform);
        
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        Image handleImage = handle.AddComponent<Image>();
        
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(20, 20);
        
        handleImage.color = Color.white;
        
        // Setup slider
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = value;
        slider.targetGraphic = backgroundImage;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        
        return sliderObj;
    }
    
    #endregion
    
    #region Toggle Creation
    
    public static GameObject CreateToggle(string text, bool isOn = false, Transform parent = null)
    {
        GameObject toggleObj = new GameObject("Toggle");
        if (parent != null)
            toggleObj.transform.SetParent(parent);
        
        RectTransform rectTransform = toggleObj.AddComponent<RectTransform>();
        Toggle toggle = toggleObj.AddComponent<Toggle>();
        
        // Create background
        GameObject background = new GameObject("Background");
        background.transform.SetParent(toggleObj.transform);
        
        RectTransform backgroundRect = background.AddComponent<RectTransform>();
        Image backgroundImage = background.AddComponent<Image>();
        
        backgroundRect.anchorMin = new Vector2(0, 0.5f);
        backgroundRect.anchorMax = new Vector2(0, 0.5f);
        backgroundRect.sizeDelta = new Vector2(20, 20);
        backgroundRect.anchoredPosition = new Vector2(10, 0);
        
        backgroundImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        
        // Create checkmark
        GameObject checkmark = new GameObject("Checkmark");
        checkmark.transform.SetParent(background.transform);
        
        RectTransform checkmarkRect = checkmark.AddComponent<RectTransform>();
        Image checkmarkImage = checkmark.AddComponent<Image>();
        
        checkmarkRect.anchorMin = Vector2.zero;
        checkmarkRect.anchorMax = Vector2.one;
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;
        
        checkmarkImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);
        
        // Create label
        GameObject label = new GameObject("Label");
        label.transform.SetParent(toggleObj.transform);
        
        RectTransform labelRect = label.AddComponent<RectTransform>();
        TextMeshProUGUI labelText = label.AddComponent<TextMeshProUGUI>();
        
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.offsetMin = new Vector2(40, 0);
        labelRect.offsetMax = new Vector2(0, 0);
        
        labelText.text = text;
        labelText.fontSize = 14;
        labelText.color = Color.white;
        
        // Setup toggle
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmarkImage;
        toggle.isOn = isOn;
        
        return toggleObj;
    }
    
    #endregion
    
    #region Dropdown Creation
    
    public static GameObject CreateDropdown(string[] options, Transform parent = null)
    {
        GameObject dropdownObj = new GameObject("Dropdown");
        if (parent != null)
            dropdownObj.transform.SetParent(parent);
        
        RectTransform rectTransform = dropdownObj.AddComponent<RectTransform>();
        Image image = dropdownObj.AddComponent<Image>();
        TMP_Dropdown dropdown = dropdownObj.AddComponent<TMP_Dropdown>();
        
        // Set background
        image.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        
        // Create label
        GameObject label = new GameObject("Label");
        label.transform.SetParent(dropdownObj.transform);
        
        RectTransform labelRect = label.AddComponent<RectTransform>();
        TextMeshProUGUI labelText = label.AddComponent<TextMeshProUGUI>();
        
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10, 0);
        labelRect.offsetMax = new Vector2(-30, 0);
        
        labelText.text = options.Length > 0 ? options[0] : "";
        labelText.fontSize = 14;
        labelText.color = Color.white;
        
        // Create template
        GameObject template = new GameObject("Template");
        template.transform.SetParent(dropdownObj.transform);
        template.SetActive(false);
        
        // Setup dropdown
        dropdown.options.Clear();
        foreach (string option in options)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        }
        dropdown.captionText = labelText;
        dropdown.template = template.GetComponent<RectTransform>();
        
        return dropdownObj;
    }
    
    #endregion
    
    #region Layout Helpers
    
    public static GameObject CreateHorizontalLayout(Transform parent = null)
    {
        GameObject layoutObj = new GameObject("HorizontalLayout");
        if (parent != null)
            layoutObj.transform.SetParent(parent);
        
        RectTransform rectTransform = layoutObj.AddComponent<RectTransform>();
        HorizontalLayoutGroup layoutGroup = layoutObj.AddComponent<HorizontalLayoutGroup>();
        
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.spacing = 10f;
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);
        
        return layoutObj;
    }
    
    public static GameObject CreateVerticalLayout(Transform parent = null)
    {
        GameObject layoutObj = new GameObject("VerticalLayout");
        if (parent != null)
            layoutObj.transform.SetParent(parent);
        
        RectTransform rectTransform = layoutObj.AddComponent<RectTransform>();
        VerticalLayoutGroup layoutGroup = layoutObj.AddComponent<VerticalLayoutGroup>();
        
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.spacing = 10f;
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);
        
        return layoutObj;
    }
    
    public static GameObject CreateGridLayout(int columns, Transform parent = null)
    {
        GameObject layoutObj = new GameObject("GridLayout");
        if (parent != null)
            layoutObj.transform.SetParent(parent);
        
        RectTransform rectTransform = layoutObj.AddComponent<RectTransform>();
        GridLayoutGroup layoutGroup = layoutObj.AddComponent<GridLayoutGroup>();
        
        layoutGroup.cellSize = new Vector2(100, 30);
        layoutGroup.spacing = new Vector2(10, 10);
        layoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layoutGroup.constraintCount = columns;
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);
        
        return layoutObj;
    }
    
    #endregion
    
    #region Utility Methods
    
    public static void SetAnchors(RectTransform rectTransform, Vector2 min, Vector2 max)
    {
        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
    
    public static void SetAnchorsAndPivot(RectTransform rectTransform, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.pivot = pivot;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
    
    public static void SetSize(RectTransform rectTransform, Vector2 size)
    {
        rectTransform.sizeDelta = size;
    }
    
    public static void SetPosition(RectTransform rectTransform, Vector2 position)
    {
        rectTransform.anchoredPosition = position;
    }
    
    #endregion
} 