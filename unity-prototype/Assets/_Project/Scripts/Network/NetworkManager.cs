using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("Network Settings")]
    public string gameVersion = "1.0";
    public int maxPlayersPerRoom = 2;
    public bool autoConnect = true;
    public float connectionTimeout = 10f;
    
    [Header("Game Settings")]
    public string roomName = "SamuraiBattle";
    public bool isHost = false;
    public bool isConnected = false;
    public bool isInRoom = false;
    
    [Header("Network State")]
    public NetworkState currentState = NetworkState.Disconnected;
    public string currentRoomName = "";
    public List<PlayerInfo> connectedPlayers = new List<PlayerInfo>();
    
    [Header("Rollback Settings")]
    public int maxRollbackFrames = 7;
    public int inputDelay = 2;
    public bool useRollbackNetcode = true;
    
    private static NetworkManager instance;
    public static NetworkManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<NetworkManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("NetworkManager");
                    instance = go.AddComponent<NetworkManager>();
                }
            }
            return instance;
        }
    }

    // Events
    public System.Action OnConnectedToServer;
    public System.Action OnDisconnectedFromServer;
    public System.Action OnRoomJoined;
    public System.Action OnRoomLeft;
    public System.Action<PlayerInfo> OnPlayerJoined;
    public System.Action<PlayerInfo> OnPlayerLeft;
    public System.Action OnGameReady;
    public System.Action OnGameStart;
    public System.Action OnGameEnd;

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

    private void Start()
    {
        if (autoConnect)
        {
            ConnectToServer();
        }
    }

    public void ConnectToServer()
    {
        if (isConnected) return;
        
        currentState = NetworkState.Connecting;
        Debug.Log("Connecting to Photon Server...");
        
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.ConnectUsingSettings();
        
        StartCoroutine(ConnectionTimeout());
    }

    private IEnumerator ConnectionTimeout()
    {
        yield return new WaitForSeconds(connectionTimeout);
        
        if (currentState == NetworkState.Connecting)
        {
            Debug.LogWarning("Connection timeout!");
            currentState = NetworkState.Disconnected;
            OnDisconnectedFromServer?.Invoke();
        }
    }

    public void DisconnectFromServer()
    {
        if (!isConnected) return;
        
        Debug.Log("Disconnecting from Photon Server...");
        PhotonNetwork.Disconnect();
    }

    public void CreateRoom(string roomName = "")
    {
        if (!isConnected) return;
        
        string roomToCreate = string.IsNullOrEmpty(roomName) ? this.roomName : roomName;
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true
        };
        
        Debug.Log($"Creating room: {roomToCreate}");
        PhotonNetwork.CreateRoom(roomToCreate, roomOptions);
    }

    public void JoinRoom(string roomName)
    {
        if (!isConnected) return;
        
        Debug.Log($"Joining room: {roomName}");
        PhotonNetwork.JoinRoom(roomName);
    }

    public void JoinRandomRoom()
    {
        if (!isConnected) return;
        
        Debug.Log("Joining random room...");
        PhotonNetwork.JoinRandomRoom();
    }

    public void LeaveRoom()
    {
        if (!isInRoom) return;
        
        Debug.Log("Leaving room...");
        PhotonNetwork.LeaveRoom();
    }

    public void StartGame()
    {
        if (!isInRoom || !PhotonNetwork.IsMasterClient) return;
        
        Debug.Log("Starting game...");
        photonView.RPC("RPC_StartGame", RpcTarget.All);
    }

    public void EndGame()
    {
        if (!isInRoom || !PhotonNetwork.IsMasterClient) return;
        
        Debug.Log("Ending game...");
        photonView.RPC("RPC_EndGame", RpcTarget.All);
    }

    public bool IsMasterClient()
    {
        return PhotonNetwork.IsMasterClient;
    }

    public int GetPlayerCount()
    {
        return PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
    }

    public PlayerInfo GetLocalPlayer()
    {
        if (PhotonNetwork.LocalPlayer == null) return null;
        
        return new PlayerInfo
        {
            actorNumber = PhotonNetwork.LocalPlayer.ActorNumber,
            actorId = PhotonNetwork.LocalPlayer.ActorNumber,
            playerName = PhotonNetwork.LocalPlayer.NickName,
            isLocal = true,
            isReady = true
        };
    }

    public List<PlayerInfo> GetConnectedPlayers()
    {
        List<PlayerInfo> players = new List<PlayerInfo>();
        
        foreach (var player in PhotonNetwork.PlayerList)
        {
            players.Add(new PlayerInfo
            {
                actorNumber = player.ActorNumber,
                actorId = player.ActorNumber,
                playerName = player.NickName,
                isLocal = player.IsLocal,
                isReady = true
            });
        }
        
        return players;
    }

    // Photon Callbacks
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server");
        isConnected = true;
        currentState = NetworkState.Connected;
        OnConnectedToServer?.Invoke();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Disconnected from Photon Server. Cause: {cause}");
        isConnected = false;
        isInRoom = false;
        currentState = NetworkState.Disconnected;
        connectedPlayers.Clear();
        OnDisconnectedFromServer?.Invoke();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");
        isInRoom = true;
        currentRoomName = PhotonNetwork.CurrentRoom.Name;
        currentState = NetworkState.InRoom;
        // Update connected players
        connectedPlayers = GetConnectedPlayers();
        OnRoomJoined?.Invoke();
        // Notify other players
        photonView.RPC("RPC_PlayerJoined", RpcTarget.Others, GetLocalPlayer());
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left room");
        isInRoom = false;
        currentRoomName = "";
        currentState = NetworkState.Connected;
        connectedPlayers.Clear();
        OnRoomLeft?.Invoke();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player joined: {newPlayer.NickName}");
        
        PlayerInfo playerInfo = new PlayerInfo
        {
            actorNumber = newPlayer.ActorNumber,
            actorId = newPlayer.ActorNumber,
            playerName = newPlayer.NickName,
            isLocal = false,
            isReady = true
        };
        
        connectedPlayers.Add(playerInfo);
        OnPlayerJoined?.Invoke(playerInfo);
        
        // Notify the new player about existing players
        photonView.RPC("RPC_PlayerJoined", newPlayer, GetLocalPlayer());
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player left: {otherPlayer.NickName}");
        
        PlayerInfo playerInfo = connectedPlayers.Find(p => p.actorNumber == otherPlayer.ActorNumber);
        if (playerInfo != null)
        {
            connectedPlayers.Remove(playerInfo);
            OnPlayerLeft?.Invoke(playerInfo);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join room. Code: {returnCode}, Message: {message}");
        currentState = NetworkState.Connected;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to create room. Code: {returnCode}, Message: {message}");
        currentState = NetworkState.Connected;
    }

    // RPC Methods
    [PunRPC]
    private void RPC_PlayerJoined(PlayerInfo playerInfo)
    {
        if (!connectedPlayers.Exists(p => p.actorNumber == playerInfo.actorNumber))
        {
            connectedPlayers.Add(playerInfo);
            OnPlayerJoined?.Invoke(playerInfo);
        }
    }

    [PunRPC]
    private void RPC_StartGame()
    {
        Debug.Log("Game started!");
        currentState = NetworkState.GameStarted;
        OnGameStart?.Invoke();
    }

    [PunRPC]
    private void RPC_EndGame()
    {
        Debug.Log("Game ended!");
        currentState = NetworkState.InRoom;
        OnGameEnd?.Invoke();
    }

    public void SetPlayerReady(bool ready)
    {
        if (!isInRoom) return;
        
        ExitGames.Client.Photon.Hashtable customProperties = new ExitGames.Client.Photon.Hashtable();
        customProperties["Ready"] = ready;
        PhotonNetwork.LocalPlayer.SetCustomProperties(customProperties);
    }

    public bool AreAllPlayersReady()
    {
        if (!isInRoom) return false;
        
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.ContainsKey("Ready"))
            {
                bool isReady = (bool)player.CustomProperties["Ready"];
                if (!isReady) return false;
            }
            else
            {
                return false;
            }
        }
        
        return true;
    }
}

[System.Serializable]
public class PlayerInfo
{
    public int actorNumber;
    public int actorId; // Field instead of property
    public string playerName;
    public string characterName;
    public int skillLevel;
    public string region;
    public bool isLocal;
    public bool isReady;
    public int characterId = -1;
}

public enum NetworkState
{
    Disconnected,
    Connecting,
    Connected,
    InRoom,
    GameStarted,
    GameEnded
} 