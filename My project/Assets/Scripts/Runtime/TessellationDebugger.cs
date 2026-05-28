using UnityEngine;

/// Draws the hidden triangular tessellation as Scene-view gizmos.
/// Attach to any GameObject. Disable or remove before final builds.
public class TessellationDebugger : MonoBehaviour
{
    [Header("Display")]
    public bool  showEdges        = true;
    public bool  showCentroids    = false;
    public bool  showAdjacency    = false;

    [Header("Colors")]
    public Color edgeColor        = new Color(1f, 1f, 1f, 0.4f);
    public Color currentTriColor  = Color.cyan;
    public Color adjLineColor     = new Color(1f, 0.5f, 0f, 0.5f);
    public Color centroidColor    = Color.yellow;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var tess = ArenaLoader.Tessellation;
        if (tess == null) return;

        int cur = ArenaLoader.CurrentTriangle;

        for (int i = 0; i < tess.TriCount; i++)
        {
            var v = tess.Verts[i];
            if (v == null) continue;

            var offset = Vector3.up * 0.06f;
            bool isCur = (i == cur);

            if (showEdges)
            {
                Gizmos.color = isCur ? currentTriColor : edgeColor;
                Gizmos.DrawLine(v[0] + offset, v[1] + offset);
                Gizmos.DrawLine(v[1] + offset, v[2] + offset);
                Gizmos.DrawLine(v[2] + offset, v[0] + offset);
            }

            if (showCentroids)
            {
                Gizmos.color = centroidColor;
                Gizmos.DrawSphere(tess.Centroids[i] + offset, 0.08f);
            }

            if (showAdjacency && tess.Adj != null)
            {
                Gizmos.color = adjLineColor;
                foreach (int nb in tess.Adj[i])
                    if (nb > i)
                        Gizmos.DrawLine(tess.Centroids[i] + offset,
                                        tess.Centroids[nb] + offset);
            }
        }
    }
#endif
}
