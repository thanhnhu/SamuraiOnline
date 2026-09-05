using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class BackgroundManager : MonoBehaviour
{
    [Header("Background Settings")]
    public SpriteRenderer backgroundRenderer;
    public List<string> availableBackgrounds = new List<string>();
    
    [Header("Current Background")]
    public string currentBackground = "B001";
    public Texture2D currentBackgroundTexture;
    
    [Header("Background Info")]
    public int backgroundWidth = 640;
    public int backgroundHeight = 480;
    
    private Dictionary<string, Texture2D> loadedBackgrounds = new Dictionary<string, Texture2D>();
    
    private void Start()
    {
        InitializeBackgroundList();
        LoadBackground(currentBackground);
    }
    
    private void InitializeBackgroundList()
    {
        // List of available backgrounds from original game
        availableBackgrounds.AddRange(new string[]
        {
            "B001", "B002", "B003", "B004", "B005", "B007", "B008", "B009", 
            "B00A", "B00C", "B00D", "B00E", "B00F", "B010", "B011", "B012", "B013",
            "B100", "B200", "B300", "B400", "B500"
        });
    }
    
    public void LoadBackground(string backgroundName)
    {
        // Try to load from cache first
        if (loadedBackgrounds.ContainsKey(backgroundName))
        {
            ApplyBackground(loadedBackgrounds[backgroundName]);
            return;
        }
        
        // Try to load converted PNG file first
        string pngPath = $"Backgrounds/{backgroundName}";
        Texture2D pngTexture = Resources.Load<Texture2D>(pngPath);
        
        if (pngTexture != null)
        {
            loadedBackgrounds[backgroundName] = pngTexture;
            ApplyBackground(pngTexture);
            currentBackground = backgroundName;
            return;
        }
        
        // If no PNG exists, try to convert from BGR file
        if (TryConvertBGRToTexture(backgroundName, out Texture2D convertedTexture))
        {
            loadedBackgrounds[backgroundName] = convertedTexture;
            ApplyBackground(convertedTexture);
            currentBackground = backgroundName;
            return;
        }
        
        // Fallback: create a solid color background
        Debug.LogWarning($"Could not load background {backgroundName}, using fallback");
        CreateFallbackBackground(backgroundName);
    }
    
    private bool TryConvertBGRToTexture(string backgroundName, out Texture2D texture)
    {
        texture = null;
        
        try
        {
            // Load BGR file as TextAsset
            string bgrPath = backgroundName + ".BGR";
            TextAsset bgrFile = Resources.Load<TextAsset>(bgrPath);
            
            if (bgrFile == null)
            {
                Debug.LogWarning($"BGR file not found: {bgrPath}");
                return false;
            }
            
            // For now, create a procedural background based on the BGR name
            // This is a placeholder until we implement proper BGR decoding
            texture = CreateProceduralBackground(backgroundName, bgrFile.bytes.Length);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error converting BGR file {backgroundName}: {e.Message}");
            return false;
        }
    }
    
    private Texture2D CreateProceduralBackground(string backgroundName, int dataSize)
    {
        Texture2D texture = new Texture2D(backgroundWidth, backgroundHeight, TextureFormat.RGB24, false);
        
        // Create different backgrounds based on name/data
        Color32[] pixels = new Color32[backgroundWidth * backgroundHeight];
        
        // Generate background based on name
        switch (backgroundName)
        {
            case "B001": // Dojo style
                FillGradientBackground(pixels, new Color32(40, 30, 20, 255), new Color32(80, 60, 40, 255));
                break;
            case "B002": // Forest
                FillGradientBackground(pixels, new Color32(20, 40, 20, 255), new Color32(40, 80, 40, 255));
                break;
            case "B003": // Mountain
                FillGradientBackground(pixels, new Color32(60, 50, 40, 255), new Color32(120, 100, 80, 255));
                break;
            case "B004": // Temple
                FillGradientBackground(pixels, new Color32(50, 40, 30, 255), new Color32(100, 80, 60, 255));
                break;
            case "B005": // Night scene
                FillGradientBackground(pixels, new Color32(20, 20, 40, 255), new Color32(40, 40, 80, 255));
                break;
            case "B100": // Arena
                FillGradientBackground(pixels, new Color32(60, 40, 20, 255), new Color32(120, 80, 40, 255));
                break;
            case "B200": // Castle
                FillGradientBackground(pixels, new Color32(40, 40, 50, 255), new Color32(80, 80, 100, 255));
                break;
            case "B300": // Desert
                FillGradientBackground(pixels, new Color32(80, 60, 30, 255), new Color32(160, 120, 60, 255));
                break;
            default:
                // Random based on data size
                int hash = backgroundName.GetHashCode() + dataSize;
                Random.InitState(hash);
                Color32 color1 = new Color32((byte)Random.Range(30, 80), (byte)Random.Range(30, 80), (byte)Random.Range(30, 80), 255);
                Color32 color2 = new Color32((byte)(color1.r * 2), (byte)(color1.g * 2), (byte)(color1.b * 2), 255);
                FillGradientBackground(pixels, color1, color2);
                break;
        }
        
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.name = backgroundName;
        
        return texture;
    }
    
    private void FillGradientBackground(Color32[] pixels, Color32 topColor, Color32 bottomColor)
    {
        for (int y = 0; y < backgroundHeight; y++)
        {
            float t = (float)y / backgroundHeight;
            Color32 lineColor = Color32.Lerp(bottomColor, topColor, t);
            
            for (int x = 0; x < backgroundWidth; x++)
            {
                pixels[y * backgroundWidth + x] = lineColor;
            }
        }
    }
    
    private void CreateFallbackBackground(string backgroundName)
    {
        Texture2D fallbackTexture = new Texture2D(backgroundWidth, backgroundHeight, TextureFormat.RGB24, false);
        Color32[] pixels = new Color32[backgroundWidth * backgroundHeight];
        
        // Create a simple checkered pattern
        for (int y = 0; y < backgroundHeight; y++)
        {
            for (int x = 0; x < backgroundWidth; x++)
            {
                bool checker = ((x / 32) + (y / 32)) % 2 == 0;
                pixels[y * backgroundWidth + x] = checker ? new Color32(50, 50, 50, 255) : new Color32(70, 70, 70, 255);
            }
        }
        
        fallbackTexture.SetPixels32(pixels);
        fallbackTexture.Apply();
        fallbackTexture.name = backgroundName + "_fallback";
        
        loadedBackgrounds[backgroundName] = fallbackTexture;
        ApplyBackground(fallbackTexture);
        currentBackground = backgroundName;
    }
    
    private void ApplyBackground(Texture2D texture)
    {
        if (backgroundRenderer != null && texture != null)
        {
            // Create sprite from texture
            Sprite backgroundSprite = Sprite.Create(
                texture, 
                new Rect(0, 0, texture.width, texture.height), 
                new Vector2(0.5f, 0.5f), 
                100f
            );
            
            backgroundRenderer.sprite = backgroundSprite;
            currentBackgroundTexture = texture;
            
            // Scale to fit screen
            float screenHeight = Camera.main.orthographicSize * 2;
            float screenWidth = screenHeight * Camera.main.aspect;
            
            float scaleX = screenWidth / (texture.width / 100f);
            float scaleY = screenHeight / (texture.height / 100f);
            float scale = Mathf.Max(scaleX, scaleY);
            
            backgroundRenderer.transform.localScale = Vector3.one * scale;
        }
    }
    
    public void NextBackground()
    {
        int currentIndex = availableBackgrounds.IndexOf(currentBackground);
        int nextIndex = (currentIndex + 1) % availableBackgrounds.Count;
        LoadBackground(availableBackgrounds[nextIndex]);
    }
    
    public void PreviousBackground()
    {
        int currentIndex = availableBackgrounds.IndexOf(currentBackground);
        int prevIndex = (currentIndex - 1 + availableBackgrounds.Count) % availableBackgrounds.Count;
        LoadBackground(availableBackgrounds[prevIndex]);
    }
    
    public List<string> GetAvailableBackgrounds()
    {
        return new List<string>(availableBackgrounds);
    }
    
    public void SetBackground(string backgroundName)
    {
        if (availableBackgrounds.Contains(backgroundName))
        {
            LoadBackground(backgroundName);
        }
        else
        {
            Debug.LogWarning($"Background {backgroundName} not available");
        }
    }
} 