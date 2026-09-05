using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class SpectatorManager : MonoBehaviourPunCallbacks
{
    [Header("Spectator Settings")]
    public int maxSpectatorsPerRoom = 10;
    public float spectatorUpdateRate = 0.1f; // 10 times per second
    public bool allowSpectators = true;
    
    [Header("Spectator UI")]
    public GameObject spectatorPanelPrefab;
    public GameObject spectatorListItemPrefab;
    public Transform spectatorUIParent;
    
    [Header("Camera Settings")]
    public Camera spectatorCamera;
    public float cameraSmoothSpeed = 5f;
    public Vector3 cameraOffset = new Vector3(0, 5, -10);
    public float cameraZoomSpeed = 2f;
    public float minZoom = 5f;
    public float maxZoom = 15f;
    
    // Spectator state
    private bool isSpectating = false;
    private string spectatingRoomName = "";
    private List<Player> spectators = new List<Player>();
    private List<Player> playersInMatch = new List<Player>();
    
    // UI references
    private GameObject spectatorPanel;
    private List<GameObject> spectatorListItems = new List<GameObject>();
    
    // Camera control
    private Vector3 targetCameraPosition;
    private float currentZoom = 10f;
    private Transform currentTarget;
    
    // Events
    public System.Action<Player> OnSpectatorJoined;
    public System.Action<Player> OnSpectatorLeft;
    public System.Action<string> OnSpectatingStarted;
    public System.Action OnSpectatingStopped;
    
    private void Start()
    {
        if (spectatorCamera == null)
        {
            spectatorCamera = Camera.main;
        }
        
        // Initialize spectator camera
        if (spectatorCamera != null)
        {
            spectatorCamera.gameObject.SetActive(false);
        }
    }
    
    private void Update()
    {
        if (isSpectating && spectatorCamera != null)
        {
            UpdateSpectatorCamera();
            HandleSpectatorInput();
        }
    }
    
    #region Public Methods
    
    public void JoinAsSpectator(string roomName)
    {
        if (!allowSpectators)
        {
            Debug.LogWarning("Spectators are not allowed in this game.");
            return;
        }
        
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        
        spectatingRoomName = roomName;
        PhotonNetwork.JoinRoom(roomName);
    }
    
    public void LeaveSpectatorMode()
    {
        if (isSpectating)
        {
            StopSpectating();
            PhotonNetwork.LeaveRoom();
        }
    }
    
    public void SwitchSpectatorTarget(Transform newTarget)
    {
        currentTarget = newTarget;
        if (currentTarget != null)
        {
            targetCameraPosition = currentTarget.position + cameraOffset;
        }
    }
    
    public void ZoomIn()
    {
        currentZoom = Mathf.Max(currentZoom - cameraZoomSpeed * Time.deltaTime, minZoom);
        UpdateCameraZoom();
    }
    
    public void ZoomOut()
    {
        currentZoom = Mathf.Min(currentZoom + cameraZoomSpeed * Time.deltaTime, maxZoom);
        UpdateCameraZoom();
    }
    
    public bool IsSpectating()
    {
        return isSpectating;
    }
    
    public string GetSpectatingRoomName()
    {
        return spectatingRoomName;
    }
    
    public List<Player> GetSpectators()
    {
        return new List<Player>(spectators);
    }
    
    public List<Player> GetPlayersInMatch()
    {
        return new List<Player>(playersInMatch);
    }
    
    #endregion
    
    #region Private Methods
    
    private void StartSpectating()
    {
        isSpectating = true;
        
        // Activate spectator camera
        if (spectatorCamera != null)
        {
            spectatorCamera.gameObject.SetActive(true);
            currentZoom = 10f;
            UpdateCameraZoom();
        }
        
        // Create spectator UI
        CreateSpectatorUI();
        
        // Find players in the match
        UpdatePlayersInMatch();
        
        // Set initial camera target
        if (playersInMatch.Count > 0)
        {
            SwitchSpectatorTarget(FindPlayerTransform(playersInMatch[0]));
        }
        
        OnSpectatingStarted?.Invoke(spectatingRoomName);
        
        Debug.Log($"Started spectating room: {spectatingRoomName}");
    }
    
    private void StopSpectating()
    {
        isSpectating = false;
        
        // Deactivate spectator camera
        if (spectatorCamera != null)
        {
            spectatorCamera.gameObject.SetActive(false);
        }
        
        // Destroy spectator UI
        DestroySpectatorUI();
        
        // Clear lists
        spectators.Clear();
        playersInMatch.Clear();
        
        OnSpectatingStopped?.Invoke();
        
        Debug.Log("Stopped spectating");
    }
    
    private void UpdateSpectatorCamera()
    {
        if (spectatorCamera == null || currentTarget == null) return;
        
        // Smooth camera movement
        Vector3 desiredPosition = currentTarget.position + cameraOffset;
        targetCameraPosition = Vector3.Lerp(targetCameraPosition, desiredPosition, cameraSmoothSpeed * Time.deltaTime);
        
        spectatorCamera.transform.position = targetCameraPosition;
        spectatorCamera.transform.LookAt(currentTarget);
    }
    
    private void UpdateCameraZoom()
    {
        if (spectatorCamera == null) return;
        
        // Adjust camera field of view based on zoom
        float targetFOV = Mathf.Lerp(60f, 30f, (currentZoom - minZoom) / (maxZoom - minZoom));
        spectatorCamera.fieldOfView = Mathf.Lerp(spectatorCamera.fieldOfView, targetFOV, Time.deltaTime * 5f);
    }
    
    private void HandleSpectatorInput()
    {
        // Camera zoom controls
        if (Input.GetKey(KeyCode.Q))
        {
            ZoomIn();
        }
        if (Input.GetKey(KeyCode.E))
        {
            ZoomOut();
        }
        
        // Switch targets with number keys
        for (int i = 0; i < playersInMatch.Count && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SwitchSpectatorTarget(FindPlayerTransform(playersInMatch[i]));
            }
        }
        
        // Leave spectator mode
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LeaveSpectatorMode();
        }
    }
    
    private void CreateSpectatorUI()
    {
        if (spectatorPanelPrefab == null || spectatorUIParent == null) return;
        
        spectatorPanel = Instantiate(spectatorPanelPrefab, spectatorUIParent);
        UpdateSpectatorUI();
    }
    
    private void DestroySpectatorUI()
    {
        if (spectatorPanel != null)
        {
            Destroy(spectatorPanel);
            spectatorPanel = null;
        }
        
        foreach (GameObject item in spectatorListItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spectatorListItems.Clear();
    }
    
    private void UpdateSpectatorUI()
    {
        if (spectatorPanel == null) return;
        
        // Update spectator count
        var countText = spectatorPanel.GetComponentInChildren<TextMeshProUGUI>();
        if (countText != null)
        {
            countText.text = $"Spectators: {spectators.Count}/{maxSpectatorsPerRoom}";
        }
        
        // Update spectator list
        UpdateSpectatorList();
    }
    
    private void UpdateSpectatorList()
    {
        // Clear existing list items
        foreach (GameObject item in spectatorListItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spectatorListItems.Clear();
        
        // Create new list items
        if (spectatorListItemPrefab != null && spectatorPanel != null)
        {
            Transform listParent = spectatorPanel.transform.Find("SpectatorList");
            if (listParent != null)
            {
                foreach (Player spectator in spectators)
                {
                    GameObject listItem = Instantiate(spectatorListItemPrefab, listParent);
                    var textComponent = listItem.GetComponentInChildren<TextMeshProUGUI>();
                    if (textComponent != null)
                    {
                        textComponent.text = spectator.NickName;
                    }
                    spectatorListItems.Add(listItem);
                }
            }
        }
    }
    
    private void UpdatePlayersInMatch()
    {
        playersInMatch.Clear();
        
        // Get all players in the room except spectators
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.ContainsKey("IsSpectator"))
            {
                bool isSpectator = (bool)player.CustomProperties["IsSpectator"];
                if (!isSpectator)
                {
                    playersInMatch.Add(player);
                }
            }
            else
            {
                // If no spectator property, assume they're a player
                playersInMatch.Add(player);
            }
        }
    }
    
    private Transform FindPlayerTransform(Player player)
    {
        // Find the player's character in the scene
        var playerCharacters = FindObjectsOfType<PlayerCharacter>();
        foreach (var character in playerCharacters)
        {
            // TODO: Implement proper player identification
            // For now, just check by name
            if (character.name.Contains(player.NickName))
            {
                return character.transform;
            }
        }
        
        // Fallback: look for any transform with the player's name
        var transforms = FindObjectsOfType<Transform>();
        foreach (var transform in transforms)
        {
            if (transform.name.Contains(player.NickName))
            {
                return transform;
            }
        }
        
        return null;
    }
    
    #endregion
    
    #region Photon Callbacks
    
    public override void OnJoinedRoom()
    {
        if (spectatingRoomName != "" && PhotonNetwork.CurrentRoom.Name == spectatingRoomName)
        {
            // Set ourselves as spectator
            var customProperties = new ExitGames.Client.Photon.Hashtable();
            customProperties["IsSpectator"] = true;
            PhotonNetwork.LocalPlayer.SetCustomProperties(customProperties);
            
            StartSpectating();
        }
    }
    
    public override void OnLeftRoom()
    {
        if (isSpectating)
        {
            StopSpectating();
        }
    }
    
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (isSpectating)
        {
            // Check if the new player is a spectator
            if (newPlayer.CustomProperties.ContainsKey("IsSpectator"))
            {
                bool isSpectator = (bool)newPlayer.CustomProperties["IsSpectator"];
                if (isSpectator)
                {
                    spectators.Add(newPlayer);
                    OnSpectatorJoined?.Invoke(newPlayer);
                }
            }
            
            UpdatePlayersInMatch();
            UpdateSpectatorUI();
        }
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (isSpectating)
        {
            // Remove from spectators list
            if (spectators.Contains(otherPlayer))
            {
                spectators.Remove(otherPlayer);
                OnSpectatorLeft?.Invoke(otherPlayer);
            }
            
            // Remove from players list
            if (playersInMatch.Contains(otherPlayer))
            {
                playersInMatch.Remove(otherPlayer);
                
                // If we were following this player, switch to another
                if (currentTarget != null && FindPlayerTransform(otherPlayer) == currentTarget)
                {
                    if (playersInMatch.Count > 0)
                    {
                        SwitchSpectatorTarget(FindPlayerTransform(playersInMatch[0]));
                    }
                }
            }
            
            UpdateSpectatorUI();
        }
    }
    
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (isSpectating && changedProps.ContainsKey("IsSpectator"))
        {
            bool isSpectator = (bool)changedProps["IsSpectator"];
            
            if (isSpectator)
            {
                if (!spectators.Contains(targetPlayer))
                {
                    spectators.Add(targetPlayer);
                    OnSpectatorJoined?.Invoke(targetPlayer);
                }
            }
            else
            {
                if (spectators.Contains(targetPlayer))
                {
                    spectators.Remove(targetPlayer);
                    OnSpectatorLeft?.Invoke(targetPlayer);
                }
            }
            
            UpdatePlayersInMatch();
            UpdateSpectatorUI();
        }
    }
    
    #endregion
} 