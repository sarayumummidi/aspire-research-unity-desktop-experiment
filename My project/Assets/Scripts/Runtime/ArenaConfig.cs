using UnityEngine;

/// Defines which triangle-edge pairs have obstacle walls in one arena.
/// Obstacle = a pair (obstacleEdgesA[i], obstacleEdgesB[i]) of adjacent triangle IDs.
[CreateAssetMenu(fileName = "ArenaConfig", menuName = "ASPIRE/Arena Config")]
public class ArenaConfig : ScriptableObject
{
    public int    arenaId;
    [TextArea(2, 4)]
    public string description;

    // Parallel arrays: each index i encodes one blocked edge between two triangles.
    public int[] obstacleEdgesA;
    public int[] obstacleEdgesB;

    public int ObstacleCount => obstacleEdgesA != null ? obstacleEdgesA.Length : 0;
}
