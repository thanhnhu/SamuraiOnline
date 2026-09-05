using UnityEditor;
using UnityEngine;

public class QuickSceneSetup : EditorWindow
{
    [MenuItem("Tools/Quick 2D Scene Setup")]
    public static void ShowWindow()
    {
        GetWindow<QuickSceneSetup>("Quick 2D Scene Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("Tạo nhanh Player và Ground cho scene 2D", EditorStyles.boldLabel);
        if (GUILayout.Button("Tạo Player + Ground"))
        {
            CreatePlayerAndGround();
        }
    }

    static void CreatePlayerAndGround()
    {
        // Tạo Ground
        GameObject ground = new GameObject("Ground");
        var groundSprite = ground.AddComponent<SpriteRenderer>();
        groundSprite.sprite = null;
        ground.transform.position = new Vector3(0, -2, 0);
        ground.transform.localScale = new Vector3(8, 1, 1);
        ground.AddComponent<BoxCollider2D>();

        // Tạo Player
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0, 0, 0);
        var playerSprite = player.AddComponent<SpriteRenderer>();
        playerSprite.sprite = null;
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<BoxCollider2D>();
        player.AddComponent<PlayerMovement>();

        // Chọn Player trong Hierarchy
        Selection.activeGameObject = player;
    }
} 