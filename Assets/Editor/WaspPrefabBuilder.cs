using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

public static class WaspPrefabBuilder
{
    public const string PrefabPath = "Assets/LevelObjects/Prefabs/Wasp.prefab";

    public static void Build()
    {
        Directory.CreateDirectory("Assets/LevelObjects/Materials");
        Material armor = EnsureLit("Assets/LevelObjects/Materials/WaspBody.mat", new Color(0.8f, 0.78f, 0.73f), Color.black, 0.28f, 0.46f);
        Material dark = EnsureLit("Assets/LevelObjects/Materials/WaspStripe.mat", new Color(0.14f, 0.15f, 0.16f), Color.black, 0.72f, 0.55f);
        Material glow = EnsureLit("Assets/LevelObjects/Materials/WaspEye.mat", new Color(0.45f, 0.82f, 1f), new Color(0.15f, 0.55f, 1f) * 3.2f, 0.05f, 0.7f);
        Material bore = EnsureLit("Assets/LevelObjects/Materials/WaspWing.mat", new Color(0.72f, 0.46f, 0.24f), new Color(0.35f, 0.14f, 0.04f), 0.55f, 0.5f);

        var root = new GameObject("Wasp");
        var pods = new List<Transform>();

        Part(root.transform, "Hull", ShapeGenerator.GenerateCube(PivotLocation.Center, Vector3.one),
            new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(0.58f, 0.26f, 0.92f), armor);
        Part(root.transform, "Deck", ShapeGenerator.GenerateCube(PivotLocation.Center, Vector3.one),
            new Vector3(0f, 0.16f, 0.02f), Quaternion.identity, new Vector3(0.4f, 0.06f, 0.48f), armor);
        Part(root.transform, "Belly", ShapeGenerator.GenerateCube(PivotLocation.Center, Vector3.one),
            new Vector3(0f, -0.14f, 0.02f), Quaternion.identity, new Vector3(0.36f, 0.08f, 0.62f), dark);
        Part(root.transform, "Nose", ShapeGenerator.GenerateCube(PivotLocation.Center, Vector3.one),
            new Vector3(0f, 0.02f, 0.56f), Quaternion.identity, new Vector3(0.34f, 0.2f, 0.26f), armor);
        Part(root.transform, "RearCan", ShapeGenerator.GenerateCylinder(PivotLocation.Center, 16, 0.5f, 1f, 1, 1),
            new Vector3(0f, 0.01f, -0.58f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.32f, 0.36f, 0.32f), armor);
        Part(root.transform, "RearVent", ShapeGenerator.GenerateCylinder(PivotLocation.Center, 12, 0.5f, 1f, 1, 0),
            new Vector3(0f, 0.01f, -0.78f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.16f, 0.06f, 0.16f), dark);
        Part(root.transform, "Sensor", ShapeGenerator.GenerateCylinder(PivotLocation.Center, 12, 0.5f, 1f, 1, 0),
            new Vector3(0f, 0.06f, 0.72f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.12f, 0.08f, 0.12f), glow);
        Part(root.transform, "Gun", ShapeGenerator.GenerateCylinder(PivotLocation.Center, 8, 0.5f, 1f, 1, 0),
            new Vector3(0f, -0.16f, 0.86f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.07f, 0.52f, 0.07f), dark);
        Part(root.transform, "GunHousing", ShapeGenerator.GenerateCube(PivotLocation.Center, Vector3.one),
            new Vector3(0f, -0.16f, 0.58f), Quaternion.identity, new Vector3(0.14f, 0.1f, 0.22f), dark);

        pods.Add(Pod(root.transform, "PodFrontLeft", new Vector3(-0.18f, 0.04f, 0.22f), new Vector3(-0.7f, 0f, 0.46f), armor, dark, bore, glow));
        pods.Add(Pod(root.transform, "PodFrontRight", new Vector3(0.18f, 0.04f, 0.22f), new Vector3(0.7f, 0f, 0.46f), armor, dark, bore, glow));
        pods.Add(Pod(root.transform, "PodRearLeft", new Vector3(-0.16f, 0.02f, -0.18f), new Vector3(-0.66f, -0.02f, -0.52f), armor, dark, bore, glow));
        pods.Add(Pod(root.transform, "PodRearRight", new Vector3(0.16f, 0.02f, -0.18f), new Vector3(0.66f, -0.02f, -0.52f), armor, dark, bore, glow));

        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, -0.16f, 1.14f);

        foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(collider);

        Wasp wasp = root.AddComponent<Wasp>();
        SerializedObject so = new SerializedObject(wasp);
        so.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
        SerializedProperty wingProp = so.FindProperty("wings");
        wingProp.arraySize = pods.Count;
        for (int i = 0; i < pods.Count; i++) wingProp.GetArrayElementAtIndex(i).objectReferenceValue = pods[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
    }

    private static Transform Pod(Transform parent, string name, Vector3 shoulder, Vector3 tip, Material armor, Material dark, Material bore, Material glow)
    {
        var gimbal = new GameObject(name);
        gimbal.transform.SetParent(parent, false);
        gimbal.transform.localPosition = shoulder;

        Vector3 localTip = tip - shoulder;
        Vector3 nozzle = new Vector3(Mathf.Sign(tip.x) * 0.9f, -0.65f, 0.22f).normalized;
        Quaternion face = Quaternion.FromToRotation(Vector3.up, nozzle);

        Part(gimbal.transform, "Strut", ShapeGenerator.GenerateCylinder(PivotLocation.Center, 6, 0.5f, 1f, 1, 0),
            localTip * 0.45f, Quaternion.FromToRotation(Vector3.up, localTip.normalized), new Vector3(0.07f, localTip.magnitude * 0.9f, 0.07f), dark);
        Part(gimbal.transform, "Housing", ShapeGenerator.GenerateCylinder(PivotLocation.Center, 16, 0.5f, 1f, 1, 1),
            localTip, face, new Vector3(0.52f, 0.2f, 0.52f), armor);
        Part(gimbal.transform, "Lip", ShapeGenerator.GenerateTorus(PivotLocation.Center, 8, 14, 0.23f, 0.04f, true, 360f, 360f, false),
            localTip + nozzle * 0.07f, face, Vector3.one, dark);
        Part(gimbal.transform, "Bore", ShapeGenerator.GenerateCylinder(PivotLocation.Center, 14, 0.5f, 1f, 1, 0),
            localTip + nozzle * 0.08f, face, new Vector3(0.34f, 0.05f, 0.34f), bore);
        Part(gimbal.transform, "Core", ShapeGenerator.GenerateCylinder(PivotLocation.Center, 12, 0.5f, 1f, 1, 0),
            localTip + nozzle * 0.1f, face, new Vector3(0.18f, 0.04f, 0.18f), glow);
        return gimbal.transform;
    }

    private static ProBuilderMesh Part(Transform parent, string name, ProBuilderMesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Material mat)
    {
        mesh.gameObject.name = name;
        mesh.transform.SetParent(parent, false);
        mesh.transform.localPosition = position;
        mesh.transform.localRotation = rotation;
        mesh.transform.localScale = scale;
        mesh.GetComponent<MeshRenderer>().sharedMaterial = mat;
        mesh.ToMesh();
        mesh.Refresh();
        return mesh;
    }

    private static Material EnsureLit(string path, Color color, Color emission, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0.001f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
