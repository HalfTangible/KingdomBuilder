using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
namespace TerrainGenerator2D
{
    public class TerrainGenerator : MonoBehaviour
    {
        [Header("Map Settings")]
        public int mapHeight = 25;
        public int mapWidth = 80;
        public int seed = 42;
        public int numRivers = 8;
        public int numTowns = 12;

        [Header("Auto Generate")]
        public bool autoGenerateOnStart = true;

        [Header("Tile Setup")]
        public TileBase baseTile;
        public Tilemap targetTilemap;

        private Tilemap tilemap;

        void Start()
        {
            if (autoGenerateOnStart)
            {
                GenerateMap();
            }
        }

        [ContextMenu("Generate Map")]
        public void GenerateMap()
        {
            Random.InitState(seed);

            // Generate heightmap using multi-octave Perlin noise (similar to Python's filtered noise)
            float[,] hm = new float[mapHeight, mapWidth];
            float amplitude = 1.0f;
            float frequency = 0.02f;  // Tuned for natural continents on 25x80 map
            float lacunarity = 2.0f;
            float persistence = 0.5f;
            int octaves = 6;
            float offsetX = (float)seed * 123.456f;
            float offsetY = (float)seed * 654.321f;

            for (int o = 0; o < octaves; o++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    for (int x = 0; x < mapWidth; x++)
                    {
                        float nx = (x + offsetX) * frequency;
                        float ny = (y + offsetY) * frequency;
                        float noise = Mathf.PerlinNoise(nx, ny) * 2f - 1f;
                        hm[y, x] += amplitude * noise;
                    }
                }
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            // Normalize heightmap to 0-1
            float minH = float.MaxValue;
            float maxH = float.MinValue;
            for (int y = 0; y < mapHeight; y++)
                for (int x = 0; x < mapWidth; x++)
                {
                    minH = Mathf.Min(minH, hm[y, x]);
                    maxH = Mathf.Max(maxH, hm[y, x]);
                }
            float range = maxH - minH;
            if (range > 0f)
                for (int y = 0; y < mapHeight; y++)
                    for (int x = 0; x < mapWidth; x++)
                        hm[y, x] = (hm[y, x] - minH) / range;

            // Classify terrain
            char[,] terrain = new char[mapHeight, mapWidth];
            for (int y = 0; y < mapHeight; y++)
                for (int x = 0; x < mapWidth; x++)
                {
                    float h = hm[y, x];
                    if (h < 0.12f) terrain[y, x] = '~';
                    else if (h < 0.22f) terrain[y, x] = '.';
                    else if (h < 0.42f) terrain[y, x] = ',';
                    else if (h < 0.62f) terrain[y, x] = 'T';
                    else if (h < 0.82f) terrain[y, x] = '^';
                    else terrain[y, x] = 'M';
                }

            // Add rivers (downhill particle flow)
            Vector2Int[] directions = {
            new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(-1, -1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(1, 1)
        };
            for (int r = 0; r < numRivers; r++)
            {
                // Find high elevation starts
                List<Vector2Int> starts = new List<Vector2Int>();
                for (int y = 0; y < mapHeight; y++)
                    for (int x = 0; x < mapWidth; x++)
                        if (hm[y, x] > 0.75f)
                            starts.Add(new Vector2Int(x, y));
                if (starts.Count == 0) continue;

                int startIdx = Random.Range(0, starts.Count);
                Vector2Int pos = starts[startIdx];
                HashSet<string> visited = new HashSet<string>();
                int pathLength = 0;
                int maxPath = 100;

                while (pathLength < maxPath)
                {
                    int py = pos.y;
                    int px = pos.x;
                    string key = $"{py},{px}";
                    if (visited.Contains(key)) break;
                    visited.Add(key);

                    // Find neighbors
                    List<(Vector2Int, float)> neighbors = new List<(Vector2Int, float)>();
                    foreach (Vector2Int d in directions)
                    {
                        int ny = py + d.y;
                        int nx = px + d.x;
                        if (ny >= 0 && ny < mapHeight && nx >= 0 && nx < mapWidth)
                        {
                            neighbors.Add((new Vector2Int(nx, ny), hm[ny, nx]));
                        }
                    }
                    if (neighbors.Count == 0) break;

                    // Lowest height neighbor
                    var bestNeigh = neighbors[0];
                    foreach (var n in neighbors)
                        if (n.Item2 < bestNeigh.Item2)
                            bestNeigh = n;

                    if (bestNeigh.Item2 >= hm[py, px]) break;

                    // Move and draw on CURRENT position
                    pos = bestNeigh.Item1;
                    terrain[py, px] = '=';
                    pathLength++;
                    if (hm[py, px] < 0.25f) break;
                }
            }

            // Place towns on suitable land (plains/light forest)
            List<Vector2Int> suitable = new List<Vector2Int>();
            for (int y = 0; y < mapHeight; y++)
                for (int x = 0; x < mapWidth; x++)
                {
                    float h = hm[y, x];
                    if (h >= 0.22f && h < 0.65f)
                        suitable.Add(new Vector2Int(x, y));
                }
            // Fisher-Yates shuffle (seeded)
            for (int i = suitable.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Vector2Int temp = suitable[i];
                suitable[i] = suitable[j];
                suitable[j] = temp;
            }
            int townsToPlace = Mathf.Min(numTowns, suitable.Count);
            for (int i = 0; i < townsToPlace; i++)
            {
                Vector2Int p = suitable[i];
                terrain[p.y, p.x] = '@';
            }

            // Build/populate tilemap
            SetupTilemap();

            // Terrain type to color mapping
            Dictionary<char, Color> terrainColors = new Dictionary<char, Color>
        {
            {'~', new Color(0.05f, 0.15f, 0.7f, 1f)},  // Deep water (dark blue)
            {'.', new Color(0.5f, 0.7f, 1f, 1f)},      // Shallow water/beach (light blue)
            {',', new Color(0.3f, 0.8f, 0.2f, 1f)},    // Plains (bright green)
            {'T', new Color(0.1f, 0.5f, 0.1f, 1f)},    // Forest (dark green)
            {'^', new Color(0.7f, 0.5f, 0.3f, 1f)},    // Hills (brown)
            {'M', new Color(0.45f, 0.45f, 0.45f, 1f)}, // Mountains (gray)
            {'=', new Color(0.1f, 0.4f, 1f, 1f)},      // River (vibrant blue)
            {'@', new Color(1f, 1f, 0f, 1f)}           // Town (yellow)
        };

            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    char tileType = terrain[y, x];
                    // Position: x right, y up, flip y to match Python print (y=0 at top)
                    Vector3Int cellPos = new Vector3Int(x, mapHeight - 1 - y, 0);
                    tilemap.SetTile(cellPos, baseTile);
                    tilemap.SetTileFlags(cellPos, TileFlags.None);
                    if (terrainColors.TryGetValue(tileType, out Color color))
                    {
                        tilemap.SetColor(cellPos, color);
                    }
                    else
                    {
                        tilemap.SetColor(cellPos, Color.white);
                    }
                }
            }

            // Log ASCII map to console (matches Python output)
            string mapLog = "Generated Terrain Map (Seed: " + seed + "):\n";
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    mapLog += terrain[y, x];
                }
                mapLog += "\n";
            }
            Debug.Log(mapLog + "\nLegend:\n~ deep water\n. shallow\n, plains\nT forest\n^ hills\nM mountains\n= river\n@ town");
        }

        private void SetupTilemap()
        {
            if (targetTilemap != null)
            {
                tilemap = targetTilemap;
                tilemap.ClearAllTiles();
                return;
            }

            if (tilemap != null)
            {
                tilemap.ClearAllTiles();
                return;
            }

            // Auto-create Grid + Tilemap hierarchy under this GameObject
            GameObject gridGO = new GameObject("ProceduralGrid");
            gridGO.transform.SetParent(transform);
            Grid gridComp = gridGO.AddComponent<Grid>();
            gridComp.cellSize = Vector3.one;  // 1x1 tiles

            GameObject tmGO = new GameObject("ProceduralTilemap");
            tmGO.transform.SetParent(gridGO.transform);
            tilemap = tmGO.AddComponent<Tilemap>();
            tmGO.AddComponent<TilemapRenderer>();
        }
    }
}