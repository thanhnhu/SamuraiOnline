using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class StageSelectManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Button nextStageButton;
    public Button prevStageButton;
    public Button selectStageButton;
    public Button backButton;
    public Text stageNameText;
    public Text stageDescriptionText;
    public Image stagePreviewImage;
    
    [Header("Stage Data")]
    public List<StageData> stages = new List<StageData>();
    
    private int currentStageIndex = 0;
    private BackgroundManager backgroundManager;
    
    [System.Serializable]
    public class StageData
    {
        public string stageName;
        public string backgroundID;
        public string description;
        public Sprite previewSprite;
    }
    
    private void Start()
    {
        InitializeStages();
        SetupUI();
        SetupBackgroundManager();
        UpdateStageDisplay();
    }
    
    private void InitializeStages()
    {
        // Initialize stage data based on original game backgrounds
        stages.Clear();
        
        stages.Add(new StageData { 
            stageName = "Dojo Training Hall", 
            backgroundID = "B001", 
            description = "Traditional training dojo with wooden floors and paper walls." 
        });
        
        stages.Add(new StageData { 
            stageName = "Mystic Forest", 
            backgroundID = "B002", 
            description = "Dense forest with ancient trees and hidden pathways." 
        });
        
        stages.Add(new StageData { 
            stageName = "Mountain Temple", 
            backgroundID = "B003", 
            description = "Sacred temple high in the mountains, shrouded in mist." 
        });
        
        stages.Add(new StageData { 
            stageName = "Ancient Temple", 
            backgroundID = "B004", 
            description = "Ruins of an ancient temple with stone pillars." 
        });
        
        stages.Add(new StageData { 
            stageName = "Moonlit Garden", 
            backgroundID = "B005", 
            description = "Serene garden under the light of the full moon." 
        });
        
        stages.Add(new StageData { 
            stageName = "Bamboo Grove", 
            backgroundID = "B007", 
            description = "Peaceful bamboo forest with gentle wind." 
        });
        
        stages.Add(new StageData { 
            stageName = "Bridge Over Waters", 
            backgroundID = "B008", 
            description = "Traditional bridge crossing over flowing waters." 
        });
        
        stages.Add(new StageData { 
            stageName = "Cherry Blossom Path", 
            backgroundID = "B009", 
            description = "Beautiful path lined with blooming cherry trees." 
        });
        
        stages.Add(new StageData { 
            stageName = "Warrior's Arena", 
            backgroundID = "B100", 
            description = "Grand arena where legendary battles take place." 
        });
        
        stages.Add(new StageData { 
            stageName = "Royal Castle", 
            backgroundID = "B200", 
            description = "Majestic castle with towering walls and banners." 
        });
        
        stages.Add(new StageData { 
            stageName = "Desert Fortress", 
            backgroundID = "B300", 
            description = "Ancient fortress in the heart of the desert." 
        });
        
        stages.Add(new StageData { 
            stageName = "Volcanic Crater", 
            backgroundID = "B400", 
            description = "Dangerous battlefield near an active volcano." 
        });
        
        stages.Add(new StageData { 
            stageName = "Ice Palace", 
            backgroundID = "B500", 
            description = "Frozen palace of ice and eternal winter." 
        });
    }
    
    private void SetupUI()
    {
        // Create UI if it doesn't exist
        if (nextStageButton == null)
        {
            CreateUI();
        }
        
        // Setup button events
        if (nextStageButton != null)
            nextStageButton.onClick.AddListener(NextStage);
            
        if (prevStageButton != null)
            prevStageButton.onClick.AddListener(PreviousStage);
            
        if (selectStageButton != null)
            selectStageButton.onClick.AddListener(SelectCurrentStage);
            
        if (backButton != null)
            backButton.onClick.AddListener(GoBack);
    }
    
    private void CreateUI()
    {
        // Create basic UI elements if they don't exist
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }
        
        // Create stage name text
        if (stageNameText == null)
        {
            GameObject textGO = new GameObject("StageNameText");
            textGO.transform.SetParent(canvas.transform);
            stageNameText = textGO.AddComponent<Text>();
            stageNameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            stageNameText.fontSize = 32;
            stageNameText.color = Color.white;
            stageNameText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0.8f);
            textRect.anchorMax = new Vector2(1, 0.9f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
        
        // Create description text
        if (stageDescriptionText == null)
        {
            GameObject descGO = new GameObject("StageDescriptionText");
            descGO.transform.SetParent(canvas.transform);
            stageDescriptionText = descGO.AddComponent<Text>();
            stageDescriptionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            stageDescriptionText.fontSize = 18;
            stageDescriptionText.color = Color.white;
            stageDescriptionText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform descRect = descGO.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.1f, 0.1f);
            descRect.anchorMax = new Vector2(0.9f, 0.2f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
        }
        
        // Create buttons
        CreateButton("Previous", new Vector2(0.1f, 0.5f), new Vector2(0.25f, 0.6f), PreviousStage, ref prevStageButton);
        CreateButton("Next", new Vector2(0.75f, 0.5f), new Vector2(0.9f, 0.6f), NextStage, ref nextStageButton);
        CreateButton("Select Stage", new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.4f), SelectCurrentStage, ref selectStageButton);
        CreateButton("Back", new Vector2(0.85f, 0.85f), new Vector2(0.95f, 0.95f), GoBack, ref backButton);
    }
    
    private void CreateButton(string text, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action, ref Button buttonRef)
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        
        GameObject buttonGO = new GameObject(text + "Button");
        buttonGO.transform.SetParent(canvas.transform);
        
        buttonRef = buttonGO.AddComponent<Button>();
        Image buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        RectTransform buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        
        // Add text to button
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform);
        Text buttonText = textGO.AddComponent<Text>();
        buttonText.text = text;
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.fontSize = 16;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        buttonRef.targetGraphic = buttonImage;
        buttonRef.onClick.AddListener(action);
    }
    
    private void SetupBackgroundManager()
    {
        backgroundManager = FindObjectOfType<BackgroundManager>();
        
        if (backgroundManager == null)
        {
            // Create background manager if it doesn't exist
            GameObject bgManagerGO = new GameObject("BackgroundManager");
            backgroundManager = bgManagerGO.AddComponent<BackgroundManager>();
            
            // Create background renderer
            GameObject bgRendererGO = new GameObject("BackgroundRenderer");
            bgRendererGO.transform.SetParent(bgManagerGO.transform);
            SpriteRenderer bgRenderer = bgRendererGO.AddComponent<SpriteRenderer>();
            bgRenderer.sortingOrder = -10; // Behind everything
            
            backgroundManager.backgroundRenderer = bgRenderer;
        }
    }
    
    private void UpdateStageDisplay()
    {
        if (stages.Count == 0) return;
        
        StageData currentStage = stages[currentStageIndex];
        
        // Update UI text
        if (stageNameText != null)
            stageNameText.text = currentStage.stageName;
            
        if (stageDescriptionText != null)
            stageDescriptionText.text = currentStage.description;
        
        // Update background preview
        if (backgroundManager != null)
        {
            backgroundManager.LoadBackground(currentStage.backgroundID);
        }
        
        // Update preview image if available
        if (stagePreviewImage != null && currentStage.previewSprite != null)
        {
            stagePreviewImage.sprite = currentStage.previewSprite;
        }
    }
    
    public void NextStage()
    {
        currentStageIndex = (currentStageIndex + 1) % stages.Count;
        UpdateStageDisplay();
    }
    
    public void PreviousStage()
    {
        currentStageIndex = (currentStageIndex - 1 + stages.Count) % stages.Count;
        UpdateStageDisplay();
    }
    
    public void SelectCurrentStage()
    {
        if (stages.Count > 0)
        {
            StageData selectedStage = stages[currentStageIndex];
            
            // Save selected stage to GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetSelectedStage(selectedStage.backgroundID);
            }
            
            // Save to PlayerPrefs as backup
            PlayerPrefs.SetString("SelectedStage", selectedStage.backgroundID);
            PlayerPrefs.SetString("SelectedStageName", selectedStage.stageName);
            PlayerPrefs.Save();
            
            Debug.Log($"Selected stage: {selectedStage.stageName} ({selectedStage.backgroundID})");
            
            // Load battle scene
            SceneManager.LoadScene("Battle");
        }
    }
    
    public void GoBack()
    {
        // Return to character select or main menu
        SceneManager.LoadScene("CharacterSelect");
    }
    
    private void Update()
    {
        // Keyboard controls
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            PreviousStage();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            NextStage();
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            SelectCurrentStage();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            GoBack();
        }
    }
} 