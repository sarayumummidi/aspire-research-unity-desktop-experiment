using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ASPIRE Scene Setup — run menu items in order: 0 then 1.
/// </summary>
public static class SceneSetupTool
{
    // Arena geometry — hex circumradius gives 6n² = 150 triangles at n=5, area ≈ 260 m²
    const float HEX_RADIUS       = 10f;
    const float BOUNDARY_HEIGHT  = 2.5f;
    const float WALL_THICKNESS   = 0.2f;
    const float CAMERA_HEIGHT    = 1.72f;

    // ─── menu 0 ──────────────────────────────────────────────────────────────

    [MenuItem("ASPIRE/0 - Switch to 3D URP")]
    public static void SwitchTo3DURP()
    {
        EnsureFolders();

        const string rendererPath = "Assets/Settings/ForwardRenderer.asset";
        const string pipelinePath = "Assets/Settings/UniversalRP-3D.asset";

        // Save renderer data to disk first — pipeline asset needs a persistent GUID to reference it
        AssetDatabase.DeleteAsset(rendererPath);
        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, rendererPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(rendererPath);
        rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);

        // Create 3D pipeline asset referencing the saved renderer
        AssetDatabase.DeleteAsset(pipelinePath);
        var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
        pipeline.shadowDistance = 50f;
        AssetDatabase.CreateAsset(pipeline, pipelinePath);

        // Apply to Graphics and all quality levels
        GraphicsSettings.defaultRenderPipeline = pipeline;
        int saved = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(saved, false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ASPIRE] Switched to 3D URP ForwardRenderer. You may need to reimport shaders (Assets > Reimport All).");
    }

    // ─── menu 1 ──────────────────────────────────────────────────────────────

    [MenuItem("ASPIRE/1 - Build Scene")]
    public static void BuildScene()
    {
        EnsureFolders();
        ClearScene();
        SetupSkyAndLighting();
        CreateGround();
        CreateBoundaryWalls();
        CreatePlayerRig();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[ASPIRE] Scene built. Press Ctrl+S to save the scene.");
    }

    // ─── scene element builders ───────────────────────────────────────────────

    static void ClearScene()
    {
        // GetRootGameObjects avoids deprecated FindObjectsByType overloads entirely
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            Object.DestroyImmediate(go);
    }

    static void SetupSkyAndLighting()
    {
        // Procedural skybox — replace with a mountain skybox asset later
        var skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            const string skyPath = "Assets/Materials/ProceduralSkybox.mat";
            AssetDatabase.DeleteAsset(skyPath);
            var skyMat = new Material(skyShader);
            skyMat.SetFloat("_SunSize",             0.04f);
            skyMat.SetFloat("_AtmosphereThickness", 1.0f);
            skyMat.SetColor("_SkyTint",   new Color(0.50f, 0.70f, 1.00f));
            skyMat.SetColor("_GroundColor", new Color(0.37f, 0.34f, 0.30f));
            skyMat.SetFloat("_Exposure",  1.3f);
            AssetDatabase.CreateAsset(skyMat, skyPath);
            RenderSettings.skybox = skyMat;
        }

        var sunGO = new GameObject("Sun");
        var sun   = sunGO.AddComponent<Light>();
        sun.type      = LightType.Directional;
        sun.color     = new Color(1f, 0.97f, 0.92f);
        sun.intensity = 1.15f;
        sun.shadows   = LightShadows.Soft;
        sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Trilight ambient: guaranteed fill without needing a lighting bake
        RenderSettings.ambientMode         = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.55f, 0.75f, 1.00f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.50f, 0.40f);
        RenderSettings.ambientGroundColor  = new Color(0.15f, 0.15f, 0.12f);
        RenderSettings.sun                 = sun;

        Undo.RegisterCreatedObjectUndo(sunGO, "Create Sun");
    }

    static void CreateGround()
    {
        const string meshPath = "Assets/Meshes/HexGround.asset";
        AssetDatabase.DeleteAsset(meshPath);

        var mesh = BuildHexMesh(HEX_RADIUS + WALL_THICKNESS * 0.5f);
        AssetDatabase.CreateAsset(mesh, meshPath);

        var go = new GameObject("Ground");
        go.AddComponent<MeshFilter>().sharedMesh  = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial =
            GetOrCreateMat("GrassMat", new Color(0.36f, 0.62f, 0.22f));
        go.AddComponent<MeshCollider>().sharedMesh = mesh;

        Undo.RegisterCreatedObjectUndo(go, "Create Ground");
    }

    static void CreateBoundaryWalls()
    {
        var parent = new GameObject("BoundaryWalls");
        var mat    = GetOrCreateMat("ConcreteMat", new Color(0.48f, 0.48f, 0.48f));

        for (int i = 0; i < 6; i++)
        {
            float a0 = i       * 60f * Mathf.Deg2Rad;
            float a1 = (i + 1) * 60f * Mathf.Deg2Rad;

            var v0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * HEX_RADIUS;
            var v1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * HEX_RADIUS;

            var   mid    = (v0 + v1) * 0.5f + Vector3.up * (BOUNDARY_HEIGHT * 0.5f);
            float edgeLen = Vector3.Distance(v0, v1);   // equals HEX_RADIUS for regular hex
            float yaw    = Mathf.Atan2(v1.z - v0.z, v1.x - v0.x) * Mathf.Rad2Deg;

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"BoundaryWall_{i}";
            wall.transform.SetParent(parent.transform);
            wall.transform.position   = mid;
            wall.transform.localScale = new Vector3(edgeLen + WALL_THICKNESS, BOUNDARY_HEIGHT, WALL_THICKNESS);
            wall.transform.rotation   = Quaternion.Euler(0f, -yaw, 0f);
            wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        Undo.RegisterCreatedObjectUndo(parent, "Create Boundary Walls");
    }

    static void CreatePlayerRig()
    {
        var existing = GameObject.FindWithTag("MainCamera");
        if (existing != null) Object.DestroyImmediate(existing);

        // Player root: movement controller will be added here in a later step
        var player = new GameObject("Player");
        player.transform.position = Vector3.zero;

        // Camera mounted at eye height; mouse-look will be added later
        var camGO = new GameObject("Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(player.transform);
        camGO.transform.localPosition = new Vector3(0f, CAMERA_HEIGHT, 0f);

        var cam = camGO.AddComponent<Camera>();
        cam.fieldOfView    = 70f;
        cam.nearClipPlane  = 0.05f;
        cam.farClipPlane   = 500f;

        // Simple crosshair: tiny cube at screen centre (replaced by UI Canvas crosshair in step 9)
        var crosshair = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crosshair.name = "CrosshairDot";
        Object.DestroyImmediate(crosshair.GetComponent<Collider>());
        crosshair.transform.SetParent(camGO.transform);
        crosshair.transform.localPosition = new Vector3(0f, 0f, 2f);
        crosshair.transform.localScale    = new Vector3(0.02f, 0.02f, 0.02f);
        var cr = crosshair.GetComponent<MeshRenderer>();
        cr.sharedMaterial = GetOrCreateMat("CrosshairMat", Color.white);
        cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cr.receiveShadows    = false;

        Undo.RegisterCreatedObjectUndo(player, "Create Player Rig");
    }

    // ─── mesh builder ─────────────────────────────────────────────────────────

    static Mesh BuildHexMesh(float r)
    {
        var verts = new Vector3[7];
        verts[0] = Vector3.zero;
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            verts[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
        }

        // Fan triangulation: winding reversed so normals point UP (+Y)
        var tris = new int[18];
        for (int i = 0; i < 6; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = (i + 1) % 6 + 1;
            tris[i * 3 + 2] = i + 1;
        }

        var uvs = new Vector2[7];
        uvs[0] = Vector2.one * 0.5f;
        for (int i = 0; i < 6; i++)
            uvs[i + 1] = new Vector2(verts[i + 1].x / (r * 2f) + 0.5f,
                                     verts[i + 1].z / (r * 2f) + 0.5f);

        var mesh = new Mesh { name = "HexGround" };
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.uv        = uvs;
        mesh.RecalculateNormals();
        return mesh;
    }

    // ─── asset helpers ────────────────────────────────────────────────────────

    static void EnsureFolders()
    {
        foreach (var (parent, child) in new (string parent, string child)[]
        {
            ("Assets",          "Materials"),
            ("Assets",          "Meshes"),
            ("Assets",          "Scripts"),
            ("Assets/Scripts",  "Editor"),
            ("Assets/Scripts",  "Runtime"),
        })
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }

    static Material GetOrCreateMat(string assetName, Color color)
    {
        string path = $"Assets/Materials/{assetName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
