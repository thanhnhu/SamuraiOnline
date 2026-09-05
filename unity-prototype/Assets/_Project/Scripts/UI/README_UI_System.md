# Samurai Shodown 2 Style Fighting Game - UI, Spectator & Replay System

## Overview

This document describes the comprehensive UI, spectator mode, and replay system implemented for the Samurai Shodown 2 style fighting game. The system provides a complete user interface for network play, spectator functionality, and match replay capabilities.

## System Architecture

### Core Components

1. **UIManager** - Central UI coordinator
2. **NetworkUI** - Network and matchmaking interface
3. **SpectatorUI** - Spectator mode interface
4. **ReplayUI** - Replay system interface
5. **SpectatorManager** - Spectator functionality
6. **ReplaySystem** - Replay recording and playback
7. **UIComponents** - Reusable UI components

## UI System

### UIManager
- **Location**: `Scripts/UI/UIManager.cs`
- **Purpose**: Central coordinator for all UI systems
- **Features**:
  - Panel management with smooth transitions
  - Status display and notifications
  - Loading screen management
  - Input handling (Escape key support)
  - Game state integration

### Key Features:
- **Panel Navigation**: Smooth transitions between different UI panels
- **Status System**: Real-time status updates with different types (Info, Success, Warning, Error)
- **Notification System**: Temporary notifications with auto-dismiss
- **Loading Screen**: Progress tracking and message display
- **Input Management**: Proper handling of input fields and escape key

## Network UI System

### NetworkUI
- **Location**: `Scripts/UI/NetworkUI.cs`
- **Purpose**: Complete network and matchmaking interface
- **Features**:
  - Connection status display
  - Matchmaking controls
  - Room management
  - Player settings
  - Network statistics

### Key Components:
- **Connection Panel**: Shows connection status and controls
- **Matchmaking Panel**: Find matches, create private rooms
- **Room Panel**: Room information and player management
- **Settings Panel**: Character selection, skill level, region
- **Network Info**: Ping, latency, packet loss display

## Spectator System

### SpectatorManager
- **Location**: `Scripts/Network/SpectatorManager.cs`
- **Purpose**: Handles spectator functionality
- **Features**:
  - Join matches as spectator
  - Camera controls (follow players, zoom, free camera)
  - Spectator list management
  - Network event handling

### SpectatorUI
- **Location**: `Scripts/UI/SpectatorUI.cs`
- **Purpose**: Spectator mode user interface
- **Features**:
  - Spectator list display
  - Camera control buttons
  - Match information display
  - Player statistics
  - Chat system for spectators

### Key Features:
- **Camera Controls**:
  - Follow specific players (1-9 keys)
  - Zoom in/out (Q/E keys)
  - Free camera mode
  - Smooth camera movement
- **Match Information**:
  - Real-time health bars
  - Rage meter display
  - Round information
  - Match timer
- **Spectator Chat**: Text chat for spectators
- **Player Switching**: Easy switching between player perspectives

## Replay System

### ReplaySystem
- **Location**: `Scripts/Network/ReplaySystem.cs`
- **Purpose**: Complete replay recording and playback system
- **Features**:
  - Automatic match recording
  - Frame-by-frame playback
  - Replay analysis tools
  - File management (save/load/delete)

### ReplayUI
- **Location**: `Scripts/UI/ReplayUI.cs`
- **Purpose**: Replay system user interface
- **Features**:
  - Replay list management
  - Playback controls
  - Timeline navigation
  - Analysis tools
  - Export functionality

### Key Features:
- **Recording**:
  - Automatic recording of all matches
  - Frame-by-frame data capture
  - Player input history
  - Character state tracking
  - Network event logging
- **Playback**:
  - Play/pause/stop controls
  - Frame-by-frame stepping
  - Variable playback speed (0.25x to 4x)
  - Timeline scrubbing
  - State restoration
- **Analysis**:
  - Input analysis per player
  - Network performance metrics
  - Frame rate analysis
  - Export analysis reports
- **File Management**:
  - Save replays to JSON format
  - Load replays from file
  - Delete unwanted replays
  - Search and filter replays

## UI Components

### UIComponents
- **Location**: `Scripts/UI/UIComponents.cs`
- **Purpose**: Reusable UI component factory
- **Features**:
  - Programmatic UI creation
  - Consistent styling
  - Layout helpers
  - Utility methods

### Available Components:
- **Panels**: Basic panels, scroll panels
- **Buttons**: Text buttons, icon buttons
- **Text**: Regular text, titles, subtitles
- **Input Fields**: Text input with placeholders
- **Sliders**: Value sliders with handles
- **Toggles**: Checkbox-style toggles
- **Dropdowns**: Selection dropdowns
- **Layouts**: Horizontal, vertical, grid layouts

## Data Structures

### ReplayData
```csharp
public class ReplayData
{
    public string replayId;
    public string matchId;
    public string roomName;
    public DateTime timestamp;
    public float duration;
    public int totalFrames;
    public int frameRate;
    public List<PlayerInfo> players;
    public List<ReplayFrame> frames;
    public GameSettings gameSettings;
    public string version;
}
```

### ReplayFrame
```csharp
public class ReplayFrame
{
    public int frameNumber;
    public float timestamp;
    public Dictionary<int, PlayerInput> playerInputs;
    public Dictionary<int, CharacterState> characterStates;
    public GameState gameState;
    public List<NetworkEvent> networkEvents;
}
```

### PlayerInfo
```csharp
public class PlayerInfo
{
    public int actorId;
    public string playerName;
    public string characterName;
    public int skillLevel;
    public string region;
}
```

## Usage Examples

### Starting Spectator Mode
```csharp
// Get spectator manager
SpectatorManager spectatorManager = FindObjectOfType<SpectatorManager>();

// Join a room as spectator
spectatorManager.JoinAsSpectator("RoomName123");

// Switch camera target
spectatorManager.SwitchSpectatorTarget(playerTransform);

// Zoom controls
spectatorManager.ZoomIn();
spectatorManager.ZoomOut();
```

### Recording and Playing Replays
```csharp
// Get replay system
ReplaySystem replaySystem = FindObjectOfType<ReplaySystem>();

// Load a replay
replaySystem.LoadReplay("replayId123");

// Play the replay
replaySystem.PlayReplay();

// Control playback
replaySystem.PauseReplay();
replaySystem.SetPlaybackSpeed(2f);
replaySystem.SeekToFrame(100);

// Analyze replay
ReplayAnalysis analysis = replaySystem.AnalyzeReplay(replayData);
```

### UI Management
```csharp
// Get UI manager
UIManager uiManager = FindObjectOfType<UIManager>();

// Show different panels
uiManager.ShowPanel("Network");
uiManager.ShowPanel("Spectator");
uiManager.ShowPanel("Replay");

// Show notifications
uiManager.ShowNotification("Match found!", 2f);
uiManager.ShowStatus("Connected to network", StatusType.Success);

// Show loading screen
uiManager.ShowLoadingScreen("Connecting to server...");
uiManager.UpdateLoadingProgress(0.5f, "Loading assets...");
uiManager.HideLoadingScreen();
```

## Configuration

### Spectator Settings
```csharp
// In SpectatorManager
public int maxSpectatorsPerRoom = 10;
public float spectatorUpdateRate = 0.1f;
public bool allowSpectators = true;
public float cameraSmoothSpeed = 5f;
public float cameraZoomSpeed = 2f;
```

### Replay Settings
```csharp
// In ReplaySystem
public bool autoRecordMatches = true;
public bool saveReplaysToFile = true;
public string replaySavePath = "Replays/";
public int maxReplayDuration = 300;
public float replayFrameRate = 60f;
```

## File Structure

```
Assets/_Project/Scripts/
├── UI/
│   ├── UIManager.cs
│   ├── NetworkUI.cs
│   ├── SpectatorUI.cs
│   ├── ReplayUI.cs
│   ├── UIComponents.cs
│   └── README_UI_System.md
└── Network/
    ├── SpectatorManager.cs
    └── ReplaySystem.cs
```

## Integration with Existing Systems

### Network Integration
- Works with existing NetworkManager, NetworkPlayer, NetworkInput
- Integrates with Photon PUN2 for real-time networking
- Supports rollback netcode synchronization

### Character System Integration
- Records character states and inputs
- Restores character positions and animations during replay
- Tracks health, rage meters, and combat states

### Game State Integration
- Records round information and match progress
- Tracks game settings and player configurations
- Maintains network event history

## Performance Considerations

### Spectator Mode
- Reduced update rate for spectator data
- Efficient camera movement with interpolation
- Minimal network traffic for spectator-only data

### Replay System
- Frame-based recording for deterministic playback
- Compressed data storage for large replays
- Efficient state restoration during playback
- Background recording to minimize performance impact

## Future Enhancements

### Planned Features
1. **Advanced Replay Analysis**:
   - Heat maps of player movement
   - Combo analysis and statistics
   - Frame-perfect timing analysis
   - Network lag compensation analysis

2. **Enhanced Spectator Features**:
   - Multiple camera angles
   - Picture-in-picture mode
   - Spectator tournaments
   - Live commentary system

3. **UI Improvements**:
   - Custom themes and skins
   - Accessibility options
   - Mobile-friendly layouts
   - VR spectator mode

4. **Replay Sharing**:
   - Cloud storage for replays
   - Social sharing features
   - Replay ratings and comments
   - Tournament replay archives

## Troubleshooting

### Common Issues

1. **Spectator Camera Not Working**:
   - Check if SpectatorManager is in the scene
   - Verify camera references are set
   - Ensure player characters have PhotonView components

2. **Replays Not Saving**:
   - Check replaySavePath directory exists
   - Verify file permissions
   - Ensure autoRecordMatches is enabled

3. **UI Not Showing**:
   - Check UIManager references in inspector
   - Verify panel prefabs are assigned
   - Ensure UI canvas is properly configured

### Debug Commands
```csharp
// Force start recording
replaySystem.StartRecording();

// Force stop recording
replaySystem.StopRecording();

// Join spectator mode
spectatorManager.JoinAsSpectator("TestRoom");

// Show debug info
Debug.Log($"Recording: {replaySystem.IsRecording}");
Debug.Log($"Spectating: {spectatorManager.IsSpectating()}");
```

## Conclusion

The UI, spectator, and replay system provides a comprehensive solution for modern fighting game features. It integrates seamlessly with the existing network and character systems while providing powerful tools for match analysis and community features.

The system is designed to be extensible and can be easily modified to add new features or integrate with additional systems as the game evolves. 