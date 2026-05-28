using UnityEngine;

/// Initialises the HexTessellation, spawns obstacle walls from an ArenaConfig,
/// and tracks which triangle the player occupies each frame.
[DefaultExecutionOrder(-10)]   // runs before other scripts
public class ArenaLoader : MonoBehaviour
{
    [Header("Arena")]
    public ArenaConfig config;

    [Header("Obstacle wall dimensions")]
    public float     wallHeight    = 0.4f;
    public float     wallThickness = 0.1f;
    public Material  wallMaterial;

    // ── static access ─────────────────────────────────────────────────────────

    public static HexTessellation Tessellation    { get; private set; }
    public static int             CurrentTriangle { get; private set; } = -1;

    // ── private ───────────────────────────────────────────────────────────────

    Transform _player;

    void Awake()
    {
        Tessellation = new HexTessellation(5, 10f);

        if (config != null)
            Tessellation.ApplyObstacles(config.obstacleEdgesA, config.obstacleEdgesB);

        SpawnWalls();
    }

    void Start()
    {
        var playerGO = GameObject.Find("Player");
        if (playerGO != null)
            _player = playerGO.transform;
        else
            Debug.LogWarning("[ArenaLoader] Could not find a GameObject named 'Player'.");
    }

    void Update()
    {
        if (_player != null)
            CurrentTriangle = Tessellation.GetTriangleAt(_player.position);
    }

    // ── wall placement ────────────────────────────────────────────────────────

    void SpawnWalls()
    {
        if (config == null || config.ObstacleCount == 0) return;

        var parent = new GameObject("ObstacleWalls");
        parent.transform.SetParent(transform);

        var mat = wallMaterial
            ? wallMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Lit"))
              { color = new Color(0.48f, 0.48f, 0.48f) };

        for (int e = 0; e < config.ObstacleCount; e++)
        {
            int tA = config.obstacleEdgesA[e];
            int tB = config.obstacleEdgesB[e];
            PlaceWall(tA, tB, parent.transform, mat);
        }
    }

    void PlaceWall(int tA, int tB, Transform parent, Material mat)
    {
        if (tA < 0 || tB < 0 || tA >= Tessellation.TriCount || tB >= Tessellation.TriCount)
        {
            Debug.LogWarning($"[ArenaLoader] Invalid edge ({tA},{tB}) in config — skipped.");
            return;
        }

        var (mid, dir, len) = Tessellation.GetSharedEdge(tA, tB);
        if (len < 0.01f)
        {
            Debug.LogWarning($"[ArenaLoader] Triangles {tA} and {tB} share no edge — skipped.");
            return;
        }

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = $"Wall_{tA}_{tB}";
        wall.transform.SetParent(parent);
        wall.transform.position   = mid + Vector3.up * (wallHeight * 0.5f);
        wall.transform.localScale = new Vector3(len, wallHeight, wallThickness);

        // Rotate so local X axis aligns with the edge direction
        float yaw = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        wall.transform.rotation = Quaternion.Euler(0f, -yaw, 0f);

        wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
