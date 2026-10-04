using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class FunLevelBuilder
{
    private const string HubPath = "Assets/Scenes/LevelHub.unity";
    private const string DashPath = "Assets/Scenes/DashGulch.unity";
    private const string WallPath = "Assets/Scenes/WallGallery.unity";
    private const string StairPath = "Assets/Scenes/FloatStair.unity";
    private const string WaspPath = "Assets/Scenes/WaspYard.unity";

    private static Material platformMat;
    private static Material startMat;
    private static Material goalMat;
    private static Material lavaMat;
    private static Material wallMat;
    private static Material floatMat;
    private static GameObject playerPrefab;

    [MenuItem("Tools/Build Fun Levels")]
    public static void Build()
    {
        platformMat = LoadMat("Assets/LevelObjects/Materials/Platform.mat");
        startMat = LoadMat("Assets/LevelObjects/Materials/StartPad.mat");
        goalMat = LoadMat("Assets/LevelObjects/Materials/Goal.mat");
        lavaMat = LoadMat("Assets/LevelObjects/Materials/Lava.mat");
        wallMat = LoadMat("Assets/LevelObjects/Materials/WallRun.mat");
        floatMat = LoadMat("Assets/METASystem/FloatingObject.mat");
        playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Prefabs/FirstPersonController.prefab");

        WaspPrefabBuilder.Build();
        BuildHub();
        BuildDash();
        BuildWall();
        BuildStair();
        BuildWaspYard();
        AddReturnToOriginal();
        RegisterScenes();
        EditorSceneManager.OpenScene(HubPath);
        Debug.Log("Fun levels built: LevelHub, DashGulch, WallGallery, FloatStair, WaspYard.");
    }

    private static Material LoadMat(string path)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) throw new System.InvalidOperationException("Missing material " + path);
        return mat;
    }

    private static void BuildHub()
    {
        Scene scene = NewLevelScene();
        Transform level = Root("Level");
        Lava(level, new Vector3(0f, -4f, 0f), new Vector3(40f, 2f, 40f));
        Solid(level, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(14f, 1f, 14f), startMat);

        Door(level, "Door_DashGulch", new Vector3(0f, -0.25f, 9.2f), new Vector3(4f, 0.5f, 4.8f), goalMat, "DashGulch", new Vector3(0f, 1.3f, 10.6f), new Vector3(3.2f, 2.5f, 1.6f));
        Door(level, "Door_WallGallery", new Vector3(9.2f, -0.25f, 0f), new Vector3(4.8f, 0.5f, 4f), wallMat, "WallGallery", new Vector3(10.6f, 1.3f, 0f), new Vector3(1.6f, 2.5f, 3.2f));
        Door(level, "Door_FloatStair", new Vector3(0f, -0.25f, -9.2f), new Vector3(4f, 0.5f, 4.8f), platformMat, "FloatStair", new Vector3(0f, 1.3f, -10.6f), new Vector3(3.2f, 2.5f, 1.6f));
        Door(level, "Door_PlatformerLevel", new Vector3(-9.2f, -0.25f, 0f), new Vector3(4.8f, 0.5f, 4f), platformMat, "PlatformerLevel", new Vector3(-10.6f, 1.3f, 0f), new Vector3(1.6f, 2.5f, 3.2f));

        // Pillars sit beside the walkways so each exit reads from the middle of the room.
        Solid(level, "Post_Dash", new Vector3(2.6f, 1.5f, 10.6f), new Vector3(0.35f, 3.4f, 0.35f), goalMat);
        Solid(level, "Post_Wall", new Vector3(10.6f, 1.5f, 2.6f), new Vector3(0.35f, 3.4f, 0.35f), wallMat);
        Solid(level, "Post_Stair", new Vector3(-2.6f, 1.5f, -10.6f), new Vector3(0.35f, 3.4f, 0.35f), platformMat);
        Solid(level, "Post_Original", new Vector3(-10.6f, 1.5f, -2.6f), new Vector3(0.35f, 3.4f, 0.35f), startMat);
        Solid(level, "Door_WaspYard", new Vector3(4.7f, 0.12f, 4.7f), new Vector3(3.2f, 0.28f, 3.2f), lavaMat);
        DoorTrigger(level, "Door_WaspYard_Trigger", new Vector3(4.7f, 1.4f, 4.7f), new Vector3(2.6f, 2.2f, 2.6f), "WaspYard");
        Solid(level, "Post_Wasp", new Vector3(6.3f, 1.6f, 6.3f), new Vector3(0.35f, 3.4f, 0.35f), lavaMat);

        Hint(level, "Level Select", "Ahead, green post: Dash Gulch. Right, teal post: Wall Gallery. Behind: Float Stair. Left: the original course. Red corner pad: Wasp Yard.");
        SpawnPlayer(new Vector3(0f, 1f, 0f));
        Finish(scene, HubPath);
    }

    private static void BuildDash()
    {
        Scene scene = NewLevelScene();
        Transform level = Root("Level");
        Transform platforms = Root("Platforms", level);
        Lava(level, new Vector3(0f, -4.5f, 24f), new Vector3(28f, 2f, 64f));

        // Tops: 0, 0.5, 0.5, 0.8, then the goal at 2.4. The 5m gaps need a lip dash; the last hop is short on purpose.
        Solid(platforms, "StartPad", new Vector3(0f, -0.5f, 0.5f), new Vector3(6f, 1f, 7f), startMat);
        Solid(platforms, "Warmup", new Vector3(0f, 0.25f, 8.3f), new Vector3(4f, 0.5f, 4f), platformMat);
        Solid(platforms, "DashLip", new Vector3(0f, 0.25f, 17.6f), new Vector3(3.4f, 0.5f, 5f), platformMat);
        Solid(platforms, "FloatIsland", new Vector3(0f, 0.55f, 30.5f), new Vector3(10f, 0.5f, 10f), platformMat);
        Solid(level, "Goal", new Vector3(0f, 2.15f, 40.3f), new Vector3(3.6f, 0.5f, 4.2f), goalMat);
        GoalVolume(level, new Vector3(0f, 3.4f, 40.3f), new Vector3(3f, 2.2f, 3.4f), 3.05f);
        ReturnDoor(level, new Vector3(0f, 1.2f, -2.4f), new Vector3(3f, 2.4f, 1.2f));

        FloatZone(level, platforms, new Vector3(0f, 3f, 30.5f), new Vector3(14f, 8f, 14f), 6, 1101, 1.6f,
            new Vector2(2f, 4f), new Vector2(9f, 12f), new Vector2(7f, 11f), new Vector2(4f, 7f),
            new Vector2(0.25f, 0.5f), new Vector2(3f, 5f), new Vector2(2f, 4f), new Vector2(2f, 3f), 3.2f);

        Hint(level, "Dash Gulch", "The first two gaps need a dash from the edge (Shift). Floats on the big pad can throw you to the goal or into the lava. Jump the last green pad. A dash flies past it.");
        SpawnPlayer(new Vector3(0f, 1f, 0f));
        Finish(scene, DashPath);
    }

    private static void BuildWall()
    {
        Scene scene = NewLevelScene();
        Transform level = Root("Level");
        Transform platforms = Root("Platforms", level);
        Lava(level, new Vector3(0f, -6f, 14f), new Vector3(24f, 2f, 48f));

        // Right edge of the pad stays about 0.8m from the right wall, inside the wall-run probe.
        Solid(platforms, "StartPad", new Vector3(-0.05f, -0.5f, -1f), new Vector3(3.9f, 1f, 8f), startMat);
        Solid(level, "WallRight", new Vector3(2.3f, 2.5f, 9f), new Vector3(0.5f, 12f, 14f), wallMat);
        Solid(level, "WallLeft", new Vector3(-1.95f, 2.5f, 15f), new Vector3(0.5f, 12f, 22f), wallMat);
        Solid(platforms, "Exit", new Vector3(-0.2f, -1.05f, 27f), new Vector3(5.2f, 0.5f, 6f), platformMat);
        Solid(level, "Goal", new Vector3(0f, 0.1f, 35f), new Vector3(4f, 0.5f, 4f), goalMat);
        GoalVolume(level, new Vector3(0f, 1.4f, 35f), new Vector3(3.2f, 2.2f, 3.2f), 1.0f);
        ReturnDoor(level, new Vector3(-0.6f, 1.2f, -4.4f), new Vector3(2.2f, 2.4f, 1.2f));

        FloatZone(level, platforms, new Vector3(-0.05f, 2.2f, -1f), new Vector3(6f, 5f, 10f), 4, 1602, 1.8f,
            new Vector2(2f, 4f), new Vector2(6f, 9f), new Vector2(5f, 8f), new Vector2(3f, 5f),
            new Vector2(0.2f, 0.4f), new Vector2(5f, 7f), new Vector2(3f, 5f), new Vector2(1.5f, 2.2f), 2.2f);

        Hint(level, "Wall Gallery", "Sprint beside the right wall and press Shift to stick. Jump off onto the left wall before the right wall ends. Low gravity steadies the run. Buoyancy throws you into the lava. Dash once more to the green pad.");
        SpawnPlayer(new Vector3(1.25f, 1f, -2f));
        Finish(scene, WallPath);
    }

    private static void BuildStair()
    {
        Scene scene = NewLevelScene();
        Transform level = Root("Level");
        Transform platforms = Root("Platforms", level);

        // Long lanes: buoyancy keeps you airborne until it ends, so each landing has room to drift forward.
        Solid(platforms, "Floor0", new Vector3(0f, -0.5f, -2f), new Vector3(10f, 1f, 12f), startMat);
        Solid(platforms, "Floor1", new Vector3(0f, 2.45f, 12.6f), new Vector3(8f, 0.5f, 16f), platformMat);
        Solid(platforms, "Floor2", new Vector3(0f, 5.85f, 29.2f), new Vector3(7f, 0.5f, 16f), platformMat);
        Solid(level, "Goal", new Vector3(0f, 10.55f, 45.8f), new Vector3(6f, 0.5f, 16f), goalMat);
        for (int i = 0; i < 3; i++)
        {
            float z = 8f + i * 5f;
            Catch(platforms, -7.2f, 0f, z);
            Catch(platforms, 7.2f, 0f, z);
        }
        for (int i = 0; i < 3; i++)
        {
            float z = 24f + i * 5f;
            Catch(platforms, -6.4f, 2.7f, z);
            Catch(platforms, 6.4f, 2.7f, z);
        }
        for (int i = 0; i < 3; i++)
        {
            float z = 41f + i * 5f;
            Catch(platforms, -6f, 6.1f, z);
            Catch(platforms, 6f, 6.1f, z);
        }
        Marker(level, new Vector3(0f, 0.08f, 2.4f));
        Marker(level, new Vector3(0f, 2.78f, 18.4f));
        Marker(level, new Vector3(0f, 6.18f, 35.2f));

        GoalVolume(level, new Vector3(0f, 12f, 45.8f), new Vector3(5f, 2.4f, 14f), 11.2f);
        ReturnDoor(level, new Vector3(0f, 1.2f, -7.2f), new Vector3(3f, 2.4f, 1.4f));
        Lava(level, new Vector3(0f, -5f, 22f), new Vector3(36f, 2f, 84f));

        FloatZone(level, platforms, new Vector3(0f, 6f, 22f), new Vector3(22f, 18f, 72f), 8, 2401, 1.5f,
            new Vector2(2f, 4f), new Vector2(10f, 13f), new Vector2(3f, 6f), new Vector2(3f, 5f),
            new Vector2(0.22f, 0.42f), new Vector2(4f, 6f), new Vector2(4f, 6f), new Vector2(1.8f, 2.6f), 3.5f);

        Hint(level, "Float Stair", "Each step is higher than a normal jump. Run forward from the blue mark, then touch a float. Buoyancy carries you ahead and you drop when it ends. A bad launch drops you onto the side ledges.");
        SpawnPlayer(new Vector3(0f, 1f, -5f));
        Finish(scene, StairPath);
    }

    private static void BuildWaspYard()
    {
        Scene scene = NewLevelScene();
        Transform level = Root("Level");
        Lava(level, new Vector3(0f, -4f, 8f), new Vector3(36f, 2f, 56f));
        Solid(level, "Floor", new Vector3(0f, -0.5f, 8f), new Vector3(20f, 1f, 38f), platformMat);
        Pillar(level, new Vector3(-3.2f, 2.25f, -3f));
        Pillar(level, new Vector3(3.4f, 2.25f, -1f));
        Pillar(level, new Vector3(-4.6f, 2.25f, 5f));
        Pillar(level, new Vector3(4.4f, 2.25f, 7.5f));
        Pillar(level, new Vector3(0f, 2.25f, 12f));
        Pillar(level, new Vector3(-4.2f, 2.25f, 16.5f));
        Pillar(level, new Vector3(4.6f, 2.25f, 18.5f));
        Solid(level, "Goal", new Vector3(0f, 0.35f, 23.2f), new Vector3(5f, 0.5f, 4f), goalMat);
        GoalVolume(level, new Vector3(0f, 1.9f, 23.2f), new Vector3(4.2f, 2.2f, 3.2f), 1.35f);
        ReturnDoor(level, new Vector3(0f, 1.2f, -10.2f), new Vector3(3.2f, 2.4f, 1.2f));
        PlaceWasp(new Vector3(0f, 5.4f, 4f), 4f, 0.42f, 5.4f, 0f);
        PlaceWasp(new Vector3(1.5f, 6.1f, 15f), 3.4f, -0.34f, 6.1f, 2.2f);
        Hint(level, "Wasp Yard", "The red beam is a lock. Step behind a pillar to break it. When the beam freezes, sidestep. A hit sends you back to the start.");
        SpawnPlayer(new Vector3(0f, 1f, -6f));
        Finish(scene, WaspPath);
    }

    private static void Pillar(Transform parent, Vector3 center)
    {
        Solid(parent, "Pillar", center, new Vector3(2f, 4.5f, 2f), wallMat);
    }

    private static void PlaceWasp(Vector3 position, float radius, float speed, float height, float phase)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaspPrefabBuilder.PrefabPath);
        GameObject wasp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        wasp.transform.position = position;
        SerializedObject so = new SerializedObject(wasp.GetComponent<Wasp>());
        so.FindProperty("orbitRadius").floatValue = radius;
        so.FindProperty("orbitSpeed").floatValue = speed;
        so.FindProperty("hoverHeight").floatValue = height;
        so.FindProperty("phase").floatValue = phase;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DoorTrigger(Transform parent, string name, Vector3 center, Vector3 size, string sceneName)
    {
        GameObject trigger = Trigger(parent, name, center, size);
        trigger.AddComponent<SceneDoor>();
        SerializedObject so = new SerializedObject(trigger.GetComponent<SceneDoor>());
        so.FindProperty("sceneName").stringValue = sceneName;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddReturnToOriginal()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/PlatformerLevel.unity", OpenSceneMode.Single);
        Transform level = GameObject.Find("Level").transform;
        bool changed = false;
        if (GameObject.Find("ReturnToHub") == null)
        {
            Solid(level, "HubPad", new Vector3(0f, -0.5f, -4.7f), new Vector3(3f, 1f, 3.4f), startMat);
            ReturnDoor(level, new Vector3(0f, 1.2f, -5.6f), new Vector3(2.4f, 2.4f, 1.2f));
            changed = true;
        }
        if (GameObject.Find("GoalVolume") == null)
        {
            GoalVolume(level, new Vector3(18.65f, 8.05f, 51.65f), new Vector3(3.2f, 2.2f, 3.2f), 7.2f);
            changed = true;
        }
        if (!changed) return;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void Catch(Transform parent, float x, float top, float z)
    {
        Solid(parent, "Catch", new Vector3(x, top - 0.25f, z), new Vector3(3.2f, 0.5f, 4f), wallMat);
    }

    private static void Marker(Transform parent, Vector3 position)
    {
        GameObject marker = Solid(parent, "StandHere", position, new Vector3(1.6f, 0.08f, 1.1f), startMat);
        Object.DestroyImmediate(marker.GetComponent<Collider>());
    }

    private static Scene NewLevelScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        RenderSettings.ambientMode = AmbientMode.Skybox;
        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        System.Type extra = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalLightData, Unity.RenderPipelines.Universal.Runtime");
        if (extra != null && lightGo.GetComponent(extra) == null) lightGo.AddComponent(extra);

        GameObject volumeGo = new GameObject("Global Volume");
        Volume volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.weight = 1f;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
        return scene;
    }

    private static Transform Root(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject Solid(Transform parent, string name, Vector3 center, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static void Lava(Transform parent, Vector3 center, Vector3 scale)
    {
        GameObject go = Solid(parent, "Lava", center, scale, lavaMat);
        BoxCollider box = go.GetComponent<BoxCollider>();
        box.isTrigger = true;
        go.AddComponent<Lava>();
    }

    private static void Door(Transform parent, string name, Vector3 center, Vector3 scale, Material mat, string sceneName, Vector3 triggerCenter, Vector3 triggerSize)
    {
        Solid(parent, name, center, scale, mat);
        GameObject trigger = Trigger(parent, name + "_Trigger", triggerCenter, triggerSize);
        trigger.AddComponent<SceneDoor>();
        SerializedObject so = new SerializedObject(trigger.GetComponent<SceneDoor>());
        so.FindProperty("sceneName").stringValue = sceneName;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ReturnDoor(Transform parent, Vector3 center, Vector3 size)
    {
        GameObject trigger = Trigger(parent, "ReturnToHub", center, size);
        trigger.AddComponent<SceneDoor>();
        SerializedObject so = new SerializedObject(trigger.GetComponent<SceneDoor>());
        so.FindProperty("sceneName").stringValue = "LevelHub";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void GoalVolume(Transform parent, Vector3 center, Vector3 size, float minCenterY)
    {
        GameObject trigger = Trigger(parent, "GoalVolume", center, size);
        trigger.AddComponent<GoalBanner>();
        SerializedObject so = new SerializedObject(trigger.GetComponent<GoalBanner>());
        so.FindProperty("minCenterY").floatValue = minCenterY;
        so.FindProperty("sceneName").stringValue = "LevelHub";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Trigger(Transform parent, string name, Vector3 center, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        return go;
    }

    private static void Hint(Transform parent, string title, string tip)
    {
        GameObject go = new GameObject("LevelHint");
        go.transform.SetParent(parent, false);
        go.AddComponent<LevelHint>();
        SerializedObject so = new SerializedObject(go.GetComponent<LevelHint>());
        so.FindProperty("title").stringValue = title;
        so.FindProperty("tip").stringValue = tip;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FloatZone(Transform parent, Transform platforms, Vector3 center, Vector3 size, int count, int seed,
        float replenish, Vector2 verticalHorizontal, Vector2 verticalUp, Vector2 horizontal, Vector2 horizontalUp,
        Vector2 lowGravity, Vector2 lowGravityTime, Vector2 buoyancy, Vector2 buoyancyTime, float neighbor)
    {
        GameObject go = new GameObject("FloatArea");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        FloatArea area = go.AddComponent<FloatArea>();
        SerializedObject so = new SerializedObject(area);
        so.FindProperty("size").vector3Value = size;
        so.FindProperty("platformRoot").objectReferenceValue = platforms;
        so.FindProperty("targetCount").intValue = count;
        so.FindProperty("seed").intValue = seed;
        so.FindProperty("replenishDelay").floatValue = replenish;
        so.FindProperty("neighborDistance").floatValue = neighbor;
        so.FindProperty("floatMaterial").objectReferenceValue = floatMat;
        SerializedProperty effects = so.FindProperty("effects");
        effects.FindPropertyRelative("verticalHorizontalSpeed").vector2Value = verticalHorizontal;
        effects.FindPropertyRelative("verticalUpSpeed").vector2Value = verticalUp;
        effects.FindPropertyRelative("horizontalSpeed").vector2Value = horizontal;
        effects.FindPropertyRelative("horizontalUpSpeed").vector2Value = horizontalUp;
        effects.FindPropertyRelative("lowGravityMultiplier").vector2Value = lowGravity;
        effects.FindPropertyRelative("lowGravityDuration").vector2Value = lowGravityTime;
        effects.FindPropertyRelative("buoyancyAcceleration").vector2Value = buoyancy;
        effects.FindPropertyRelative("buoyancyDuration").vector2Value = buoyancyTime;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SpawnPlayer(Vector3 position)
    {
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.transform.position = position;
        player.transform.rotation = Quaternion.identity;
        SerializedObject so = new SerializedObject(player.GetComponent<PlayerMovement>());
        so.FindProperty("canJump").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Finish(Scene scene, string path)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, path);
    }

    private static void RegisterScenes()
    {
        string[] wanted = { HubPath, DashPath, WallPath, StairPath, WaspPath, "Assets/Scenes/PlatformerLevel.unity" };
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (string path in wanted)
        {
            bool found = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != path) continue;
                found = true;
                scenes[i] = new EditorBuildSettingsScene(path, true);
                break;
            }
            if (!found) scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
