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

        [Header("Auto Generate")]
        public bool autoGenerateOnStart = true;

        [Header("Tile Setup")]
        public TileBase baseTile;
        public Tilemap targetTilemap;

        [Header("Resource-Rich Town Placement")]
        public int numTowns = 12;              // Fewer than before—more "special"
        public float minTownDistance = 8f;    // Prevents clumping
        public float townFoundingBoost = 5f;  // Guaranteed starting resources for new towns


        [Header("Attributes & Resources")]
        public bool generateResources = true;
        public int resourceDensity = 15;           // higher = more resource nodes
        public float resourceAmountMin = 2f;
        public float resourceAmountMax = 10f;

        private char[,] terrain;                   // make this a field, not local
        private TerrainData[,] mapData;            // the data grid
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
            terrain = new char[mapHeight, mapWidth];
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
            PlaceTowns();

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

        private void PlaceTowns()
        {
            // === BUILD DATA LAYER (Resources) ===
            mapData = new TerrainData[mapHeight, mapWidth];
            for (int y = 0; y < mapHeight; y++)
                for (int x = 0; x < mapWidth; x++)
                {
                    char t = terrain[y, x];
                    float weight = GetTravelWeight(t);
                    ResourceType res = ResourceType.None;
                    float amt = 0f;

                    if (generateResources && Random.value * 100f < resourceDensity)
                    {
                        res = GetRandomResource(t);
                        if (res != ResourceType.None)
                            amt = Random.Range(resourceAmountMin, resourceAmountMax);
                    }

                    mapData[y, x] = new TerrainData(t, weight, res, amt);
                }

            PlaceResourceBasedTowns();


        }

        private void PlaceResourceBasedTowns()
        {
            // 1. Find ALL buildable tiles (good terrain + water access)
            List<TownCandidate> candidates = new List<TownCandidate>();
            for (int y = 0; y < mapHeight; y++)
                for (int x = 0; x < mapWidth; x++)
                {
                    if (IsSuitableForTown(x, y))
                    {
                        float score = CalculateTownScore(x, y);
                        candidates.Add(new TownCandidate(new Vector2Int(x, y), score));
                    }
                }

            // 2. Sort by score (richest first)
            candidates.Sort((a, b) => b.score.CompareTo(a.score));

            // 3. Pick top N, with spacing
            int placed = 0;
            List<Vector2Int> townPositions = new List<Vector2Int>();
            foreach (var cand in candidates)
            {
                if (placed >= numTowns) break;

                // Check distance to existing towns
                bool tooClose = false;
                foreach (var existing in townPositions)
                {
                    if (Vector2Int.Distance(cand.pos, existing) < minTownDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                // Place town!
                terrain[cand.pos.y, cand.pos.x] = '@';
                mapData[cand.pos.y, cand.pos.x].isTown = true;

                // Give founding boost (so they don't die immediately)
                BoostTownResources(cand.pos);

                townPositions.Add(cand.pos);
                placed++;
            }

            Debug.Log($"Placed {placed} resource-rich towns (out of {numTowns} target)");
        }

        // Helper struct
        private struct TownCandidate
        {
            public Vector2Int pos;
            public float score;
            public TownCandidate(Vector2Int p, float s) { pos = p; score = s; }
        }

        private bool IsSuitableForTown(int x, int y)
        {
            char t = terrain[y, x];
            if (t == '~' || t == 'M' || t == '.') return false; // No deep water, no pure mountains, no beach-only

            // Must have water access (river or adjacent shallow)
            return HasWaterAccess(x, y);
        }

        private bool HasWaterAccess(int x, int y)
        {
            // Check self + 8 neighbors for river/shallow
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                    {
                        char nt = terrain[ny, nx];
                        if (nt == '=' || nt == '.') return true; // River or coast
                    }
                }
            return false;
        }

        private float CalculateTownScore(int x, int y)
        {
            float score = 0f;

            // Terrain bonus
            char t = terrain[y, x];
            switch (t)
            {
                case ',': score += 25f; break;  // Plains = prime farmland
                case 'T': score += 18f; break;  // Forest = lumber
                case '^': score += 12f; break;  // Hills = defensible + stone
                default: score += 5f; break;
            }

            // Resource bonus (self + 3x3 neighborhood)
            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                    {
                        var data = mapData[ny, nx];
                        if (data.resource != ResourceType.None)
                        {
                            float distFactor = 1f / (1f + Mathf.Abs(dx) + Mathf.Abs(dy)); // Closer = better
                            score += (data.resourceAmount * 3f) * distFactor;

                            // Extra love for food/fish (sustainability)
                            if (data.resource == ResourceType.Food || data.resource == ResourceType.Fish)
                                score += 30f * distFactor;
                        }
                    }
                }
            }

            return score;
        }

        private void BoostTownResources(Vector2Int pos)
        {
            // Give every new town a starter kit (food + one local resource)
            var data = mapData[pos.y, pos.x];
            data.resourceAmount += townFoundingBoost; // Local boost
            data.resource = ResourceType.Food;        // Guaranteed food

            // Also seed 2-3 nearby tiles with resources
            for (int i = 0; i < 3; i++)
            {
                int rx = pos.x + Random.Range(-4, 5);
                int ry = pos.y + Random.Range(-4, 5);
                if (rx >= 0 && rx < mapWidth && ry >= 0 && ry < mapHeight)
                {
                    var nearby = mapData[ry, rx];
                    if (nearby.resource == ResourceType.None)
                    {
                        nearby.resource = GetRandomResource(terrain[ry, rx]);
                        nearby.resourceAmount = Random.Range(4f, 8f);
                    }
                }
            }
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

        private float GetTravelWeight(char t)
        {
            switch (t)
            {
                case '~': return Mathf.Infinity;
                case '.': return 1.5f;
                case ',': return 1.0f;
                case 'T': return 1.8f;
                case '^': return 2.5f;
                case 'M': return 4.0f;
                case '=': return 0.6f;
                case '@': return 1.0f;
                default: return 1.0f;
            }
        }

        private ResourceType GetRandomResource(char t)
        {
            float roll = Random.value;
            switch (t)
            {
                case 'M':
                case '^':
                    return roll < 0.6f ? ResourceType.Iron : ResourceType.Stone;
                case 'T':
                    return ResourceType.Wood;
                case ',':
                    return roll < 0.7f ? ResourceType.Food : ResourceType.Gold;
                case '=':
                case '.':
                    return ResourceType.Fish;
                default:
                    return ResourceType.None;
            }
        }
    }
}