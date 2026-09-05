using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

public class MatchmakingManager : MonoBehaviourPunCallbacks
{
    [Header("Matchmaking Settings")]
    public float searchTimeout = 30f;
    public int maxSearchAttempts = 3;
    public bool autoAcceptMatches = true;
    public float matchAcceptTimeout = 10f;
    
    [Header("Matchmaking State")]
    public MatchmakingState currentState = MatchmakingState.Idle;
    public float searchStartTime = 0f;
    public int searchAttempts = 0;
    public string currentRoomName = "";
    
    [Header("Player Preferences")]
    public int preferredCharacterId = -1;
    public int skillLevel = 1;
    public string region = "US";
    public bool allowCrossRegion = true;
    
    [Header("Match Settings")]
    public int maxPlayers = 2;
    public float roundTime = 99f;
    public int maxRounds = 3;
    public bool rankedMatch = false;
    
    // Matchmaking data
    private List<RoomInfo> availableRooms = new List<RoomInfo>();
    private Dictionary<string, PlayerInfo> pendingMatches = new Dictionary<string, PlayerInfo>();
    private string currentMatchId = "";
    
    // Events
    public System.Action OnSearchStarted;
    public System.Action OnSearchStopped;
    public System.Action<RoomInfo> OnMatchFound;
    public System.Action<PlayerInfo> OnOpponentFound;
    public System.Action OnMatchAccepted;
    public System.Action OnMatchDeclined;
    public System.Action OnMatchReady;
    public System.Action<string> OnMatchmakingError;

    private static MatchmakingManager instance;
    public static MatchmakingManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<MatchmakingManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("MatchmakingManager");
                    instance = go.AddComponent<MatchmakingManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void StartMatchmaking()
    {
        if (currentState != MatchmakingState.Idle) return;
        
        currentState = MatchmakingState.Searching;
        searchStartTime = Time.time;
        searchAttempts = 0;
        
        Debug.Log("Starting matchmaking...");
        OnSearchStarted?.Invoke();
        
        // First try to join an existing room
        TryJoinExistingRoom();
    }

    public void StopMatchmaking()
    {
        if (currentState == MatchmakingState.Idle) return;
        
        currentState = MatchmakingState.Idle;
        searchAttempts = 0;
        availableRooms.Clear();
        pendingMatches.Clear();
        
        Debug.Log("Matchmaking stopped");
        OnSearchStopped?.Invoke();
    }

    public void CreatePrivateMatch(string roomName, string password = "")
    {
        if (!PhotonNetwork.IsConnected) return;
        
        currentState = MatchmakingState.CreatingRoom;
        currentRoomName = roomName;
        
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { "Password", password },
                { "SkillLevel", skillLevel },
                { "Region", region },
                { "Ranked", rankedMatch },
                { "RoundTime", roundTime },
                { "MaxRounds", maxRounds }
            },
            CustomRoomPropertiesForLobby = new string[] { "Password", "SkillLevel", "Region", "Ranked" }
        };
        
        Debug.Log($"Creating private match: {roomName}");
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void JoinPrivateMatch(string roomName, string password = "")
    {
        if (!PhotonNetwork.IsConnected) return;
        
        currentState = MatchmakingState.JoiningRoom;
        currentRoomName = roomName;
        
        Debug.Log($"Joining private match: {roomName}");
        PhotonNetwork.JoinRoom(roomName);
    }

    public void AcceptMatch(string matchId)
    {
        if (!pendingMatches.ContainsKey(matchId)) return;
        
        Debug.Log($"Accepting match: {matchId}");
        photonView.RPC("RPC_AcceptMatch", RpcTarget.All, matchId);
        OnMatchAccepted?.Invoke();
    }

    public void DeclineMatch(string matchId)
    {
        if (!pendingMatches.ContainsKey(matchId)) return;
        
        Debug.Log($"Declining match: {matchId}");
        photonView.RPC("RPC_DeclineMatch", RpcTarget.All, matchId);
        OnMatchDeclined?.Invoke();
        
        pendingMatches.Remove(matchId);
    }

    private void TryJoinExistingRoom()
    {
        if (availableRooms.Count > 0)
        {
            // Find the best room to join
            RoomInfo bestRoom = FindBestRoom();
            if (bestRoom != null)
            {
                JoinRoom(bestRoom);
                return;
            }
        }
        
        // No suitable room found, create a new one
        CreateNewRoom();
    }

    private RoomInfo FindBestRoom()
    {
        RoomInfo bestRoom = null;
        float bestScore = float.MinValue;
        
        foreach (var room in availableRooms)
        {
            if (!room.IsOpen || room.PlayerCount >= room.MaxPlayers) continue;
            
            float score = CalculateRoomScore(room);
            if (score > bestScore)
            {
                bestScore = score;
                bestRoom = room;
            }
        }
        
        return bestRoom;
    }

    private float CalculateRoomScore(RoomInfo room)
    {
        float score = 0f;
        
        // Prefer rooms with similar skill level
        if (room.CustomProperties.ContainsKey("SkillLevel"))
        {
            int roomSkillLevel = (int)room.CustomProperties["SkillLevel"];
            int skillDifference = Mathf.Abs(roomSkillLevel - skillLevel);
            score -= skillDifference * 10f;
        }
        
        // Prefer rooms in the same region
        if (room.CustomProperties.ContainsKey("Region"))
        {
            string roomRegion = (string)room.CustomProperties["Region"];
            if (roomRegion == region)
            {
                score += 50f;
            }
            else if (!allowCrossRegion)
            {
                score -= 1000f; // Heavily penalize cross-region
            }
        }
        
        // Prefer rooms that are almost full
        score += room.PlayerCount * 20f;
        
        return score;
    }

    private void JoinRoom(RoomInfo room)
    {
        currentState = MatchmakingState.JoiningRoom;
        currentRoomName = room.Name;
        
        Debug.Log($"Joining room: {room.Name}");
        PhotonNetwork.JoinRoom(room.Name);
    }

    private void CreateNewRoom()
    {
        currentState = MatchmakingState.CreatingRoom;
        currentRoomName = GenerateRoomName();
        
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { "SkillLevel", skillLevel },
                { "Region", region },
                { "Ranked", rankedMatch },
                { "RoundTime", roundTime },
                { "MaxRounds", maxRounds },
                { "CreatedBy", PhotonNetwork.LocalPlayer.ActorNumber }
            },
            CustomRoomPropertiesForLobby = new string[] { "SkillLevel", "Region", "Ranked" }
        };
        
        Debug.Log($"Creating new room: {currentRoomName}");
        PhotonNetwork.CreateRoom(currentRoomName, roomOptions);
    }

    private string GenerateRoomName()
    {
        return $"Samurai_{region}_{skillLevel}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
    }

    private void Update()
    {
        if (currentState == MatchmakingState.Searching)
        {
            // Check for timeout
            if (Time.time - searchStartTime > searchTimeout)
            {
                searchAttempts++;
                
                if (searchAttempts >= maxSearchAttempts)
                {
                    // Give up searching
                    OnMatchmakingError?.Invoke("Matchmaking timeout. No suitable opponents found.");
                    StopMatchmaking();
                }
                else
                {
                    // Retry
                    searchStartTime = Time.time;
                    TryJoinExistingRoom();
                }
            }
        }
    }

    // Photon Callbacks
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        availableRooms.Clear();
        
        foreach (var room in roomList)
        {
            if (room.IsVisible && room.PlayerCount < room.MaxPlayers)
            {
                availableRooms.Add(room);
            }
        }
        
        // If we're searching and found rooms, try to join one
        if (currentState == MatchmakingState.Searching && availableRooms.Count > 0)
        {
            TryJoinExistingRoom();
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");
        currentState = MatchmakingState.InRoom;
        
        // Check if we should start the match
        if (PhotonNetwork.CurrentRoom.PlayerCount >= maxPlayers)
        {
            StartMatch();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player joined: {newPlayer.NickName}");
        
        // Check if we should start the match
        if (PhotonNetwork.CurrentRoom.PlayerCount >= maxPlayers)
        {
            StartMatch();
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join room: {message}");
        OnMatchmakingError?.Invoke($"Failed to join room: {message}");
        
        if (currentState == MatchmakingState.Searching)
        {
            // Try again
            TryJoinExistingRoom();
        }
        else
        {
            currentState = MatchmakingState.Idle;
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to create room: {message}");
        OnMatchmakingError?.Invoke($"Failed to create room: {message}");
        
        if (currentState == MatchmakingState.Searching)
        {
            // Try again
            TryJoinExistingRoom();
        }
        else
        {
            currentState = MatchmakingState.Idle;
        }
    }

    private void StartMatch()
    {
        Debug.Log("Starting match...");
        currentState = MatchmakingState.MatchReady;
        OnMatchReady?.Invoke();
        
        // Notify NetworkManager to start the game
        NetworkManager.Instance?.StartGame();
    }

    // RPC Methods
    [PunRPC]
    private void RPC_AcceptMatch(string matchId)
    {
        if (matchId == currentMatchId)
        {
            Debug.Log("Match accepted by opponent");
            // Handle match acceptance
        }
    }

    [PunRPC]
    private void RPC_DeclineMatch(string matchId)
    {
        if (matchId == currentMatchId)
        {
            Debug.Log("Match declined by opponent");
            // Handle match decline
        }
    }

    public void SetPreferredCharacter(int characterId)
    {
        preferredCharacterId = characterId;
    }

    public void SetSkillLevel(int level)
    {
        skillLevel = Mathf.Clamp(level, 1, 10);
    }

    public void SetRegion(string newRegion)
    {
        region = newRegion;
    }

    public void SetAllowCrossRegion(bool allow)
    {
        allowCrossRegion = allow;
    }

    public void SetRankedMatch(bool ranked)
    {
        rankedMatch = ranked;
    }

    public MatchmakingState GetCurrentState()
    {
        return currentState;
    }

    public List<RoomInfo> GetAvailableRooms()
    {
        return new List<RoomInfo>(availableRooms);
    }

    public float GetSearchTime()
    {
        return currentState == MatchmakingState.Searching ? Time.time - searchStartTime : 0f;
    }

    public int GetSearchAttempts()
    {
        return searchAttempts;
    }
}

public enum MatchmakingState
{
    Idle,
    Searching,
    CreatingRoom,
    JoiningRoom,
    InRoom,
    MatchReady,
    MatchStarted
} 