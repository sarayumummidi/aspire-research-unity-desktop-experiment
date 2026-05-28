using System.Collections.Generic;
using UnityEngine;

/// Triangular tessellation of a regular hexagonal arena.
/// N subdivisions per hex edge → 6N² triangles total (N=5 → 150).
///
/// Triangle ID = sector * N² + localIndex
///   Upward   (▲) local indices 0 … N(N+1)/2 − 1
///   Downward (▽) local indices N(N+1)/2 … N² − 1
///
/// Within sector s (corner vectors A, B from origin):
///   Grid point P(k,m) = (k·A + m·B) / N
///   Upward(k,m):   P(k,m), P(k+1,m), P(k,m+1)     k+m ≤ N−1
///   Downward(k,m): P(k+1,m), P(k,m+1), P(k+1,m+1) k+m ≤ N−2
public class HexTessellation
{
    public readonly int   N;
    public readonly float HexRadius;
    public readonly int   TriCount;

    public readonly Vector3[][] Verts;     // [triId][0..2] world-space vertices (y=0)
    public readonly Vector3[]   Centroids;

    // Full adjacency — never mutated after construction
    public readonly List<int>[] FullAdj;

    // Runtime adjacency — obstacles remove edges via ApplyObstacles()
    public int[][] Adj { get; private set; }

    public HexTessellation(int n = 5, float hexRadius = 10f)
    {
        N         = n;
        HexRadius = hexRadius;
        TriCount  = 6 * n * n;

        Verts     = new Vector3[TriCount][];
        Centroids = new Vector3[TriCount];
        FullAdj   = new List<int>[TriCount];
        for (int i = 0; i < TriCount; i++)
            FullAdj[i] = new List<int>(3);

        for (int s = 0; s < 6; s++)
            BuildSector(s);

        // Cross-sector: sector s upward(0,m) ↔ sector s+1 upward(m,0)
        // (they share the edge m/N·A_{s+1} → (m+1)/N·A_{s+1})
        for (int s = 0; s < 6; s++)
        {
            int sBase = s * n * n;
            int sNext = ((s + 1) % 6) * n * n;
            for (int m = 0; m < n; m++)
                Link(sBase + UpIdx(0, m), sNext + UpIdx(m, 0));
        }

        ResetAdj();
    }

    // ── public API ────────────────────────────────────────────────────────────

    /// Apply obstacle edge list; resets Adj to full adjacency first.
    public void ApplyObstacles(int[] edgesA, int[] edgesB)
    {
        ResetAdj();
        if (edgesA == null) return;
        for (int e = 0; e < edgesA.Length; e++)
        {
            RemoveLink(edgesA[e], edgesB[e]);
            RemoveLink(edgesB[e], edgesA[e]);
        }
    }

    /// Triangle ID containing worldPos, or -1 if outside arena.
    public int GetTriangleAt(Vector3 worldPos)
    {
        float px = worldPos.x, pz = worldPos.z;
        float deg = Mathf.Atan2(pz, px) * Mathf.Rad2Deg;
        if (deg < 0f) deg += 360f;
        int s = Mathf.Clamp(Mathf.FloorToInt(deg / 60f), 0, 5);

        // Try the guessed sector and its two neighbours to handle boundary points
        for (int d = -1; d <= 1; d++)
        {
            int r = SolveInSector(px, pz, (s + d + 6) % 6);
            if (r >= 0) return r;
        }
        return -1;
    }

    /// World-space midpoint, direction, and length of the edge shared by tA and tB.
    public (Vector3 mid, Vector3 dir, float len) GetSharedEdge(int tA, int tB)
    {
        var shared = new List<Vector3>(2);
        foreach (var va in Verts[tA])
        foreach (var vb in Verts[tB])
            if (Vector3.SqrMagnitude(va - vb) < 1e-4f)
                shared.Add(va);

        if (shared.Count < 2)
            return (Vector3.zero, Vector3.right, 0f);

        return ((shared[0] + shared[1]) * 0.5f,
                (shared[1] - shared[0]).normalized,
                Vector3.Distance(shared[0], shared[1]));
    }

    // ── private helpers ───────────────────────────────────────────────────────

    void BuildSector(int s)
    {
        float angA = s       * 60f * Mathf.Deg2Rad;
        float angB = (s + 1) * 60f * Mathf.Deg2Rad;
        Vector3 A  = new Vector3(Mathf.Cos(angA), 0f, Mathf.Sin(angA)) * HexRadius;
        Vector3 B  = new Vector3(Mathf.Cos(angB), 0f, Mathf.Sin(angB)) * HexRadius;
        int base0  = s * N * N;

        Vector3 P(int k, int m) => (k * A + m * B) / N;

        // Upward triangles: iterate by diagonal j = k+m
        for (int j = 0; j < N; j++)
        for (int k = 0; k <= j; k++)
        {
            int m   = j - k;
            int tid = base0 + UpIdx(k, m);

            Verts[tid]     = new[] { P(k, m), P(k + 1, m), P(k, m + 1) };
            Centroids[tid] = (Verts[tid][0] + Verts[tid][1] + Verts[tid][2]) / 3f;

            // Three possible downward neighbours within the same sector
            if (k + m <= N - 2)  Link(tid, base0 + DnIdx(k,     m));     // hypotenuse
            if (k > 0)           Link(tid, base0 + DnIdx(k - 1, m));     // left edge
            if (m > 0)           Link(tid, base0 + DnIdx(k,     m - 1)); // bottom edge
        }

        // Downward triangles: geometry only (adjacency registered above)
        for (int j = 0; j <= N - 2; j++)
        for (int k = 0; k <= j; k++)
        {
            int m   = j - k;
            int tid = base0 + DnIdx(k, m);

            Verts[tid]     = new[] { P(k + 1, m), P(k, m + 1), P(k + 1, m + 1) };
            Centroids[tid] = (Verts[tid][0] + Verts[tid][1] + Verts[tid][2]) / 3f;
        }
    }

    // Upward local index: row j=k+m, column k within that row
    int UpIdx(int k, int m)
    {
        int j = k + m;
        return j * (j + 1) / 2 + k;
    }

    // Downward local index: offset past all N(N+1)/2 upward entries
    int DnIdx(int k, int m)
    {
        int j = k + m;
        return N * (N + 1) / 2 + j * (j + 1) / 2 + k;
    }

    void Link(int a, int b)
    {
        if (!FullAdj[a].Contains(b)) FullAdj[a].Add(b);
        if (!FullAdj[b].Contains(a)) FullAdj[b].Add(a);
    }

    void ResetAdj()
    {
        Adj = new int[TriCount][];
        for (int i = 0; i < TriCount; i++)
            Adj[i] = FullAdj[i].ToArray();
    }

    void RemoveLink(int from, int to)
    {
        var list = new List<int>(Adj[from]);
        list.Remove(to);
        Adj[from] = list.ToArray();
    }

    // Solve which triangle in 'sector' contains (px, pz). Returns -1 if not in sector.
    int SolveInSector(float px, float pz, int sector)
    {
        float angA = sector       * 60f * Mathf.Deg2Rad;
        float angB = (sector + 1) * 60f * Mathf.Deg2Rad;
        float ax = Mathf.Cos(angA) * HexRadius;
        float az = Mathf.Sin(angA) * HexRadius;
        float bx = Mathf.Cos(angB) * HexRadius;
        float bz = Mathf.Sin(angB) * HexRadius;

        float det = ax * bz - az * bx;
        if (Mathf.Abs(det) < 1e-6f) return -1;

        float u = (px * bz - pz * bx) / det;
        float v = (ax * pz - az * px) / det;

        if (u < -0.001f || v < -0.001f || u + v > 1.001f) return -1;
        u = Mathf.Clamp(u, 0f, 1f);
        v = Mathf.Clamp(v, 0f, 1f);

        int k  = Mathf.Min(Mathf.FloorToInt(u * N), N - 1);
        int m  = Mathf.Min(Mathf.FloorToInt(v * N), N - 1);
        float fk = u * N - k;
        float fm = v * N - m;

        int sBase = sector * N * N;

        if (fk + fm < 1f - 0.001f)
        {
            if (k + m > N - 1) return -1;
            return sBase + UpIdx(k, m);
        }
        else
        {
            if (k + m > N - 2) return -1;
            return sBase + DnIdx(k, m);
        }
    }
}
