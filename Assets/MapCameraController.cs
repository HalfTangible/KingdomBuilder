using UnityEngine;
using UnityEngine.Tilemaps;
using TerrainGenerator2D;

public class MapCameraController : MonoBehaviour
{
    [Header("Map Settings (Match TerrainGenerator)")]
    public int mapWidth = 80;
    public int mapHeight = 25;

    [Header("Camera Settings")]
    public float padding = 1.5f;  // Extra zoom-out margin
    public float minOrthoSize = 5f;
    public float maxOrthoSize = 50f;

    [Header("Auto Fit")]
    public bool autoFitOnStart = true;
    public Tilemap targetTilemap;  // Drag ProceduralTilemap here (optional)

    private Camera cam;
    private Tilemap tilemap;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (!cam) cam = Camera.main;

        if (targetTilemap) tilemap = targetTilemap;

        if (autoFitOnStart)
            FitToMap();
    }

    [ContextMenu("Fit Camera to Map")]
    public void FitToMap()
    {
        // Find ProceduralTilemap if not assigned
        if (!tilemap)
        {
            tilemap = FindObjectOfType<TerrainGenerator>()?
                .GetComponentInChildren<Tilemap>();
        }

        if (tilemap == null)
        {
            // Fallback: Use map size to center/zoom
            CenterOnMapSize();
            return;
        }

        // Get tilemap bounds in world space
        BoundsInt bounds = tilemap.cellBounds;
        // Calculate center cell (round to nearest integer cell)
        Vector3Int centerCell = new Vector3Int(
            Mathf.RoundToInt(bounds.center.x),
            Mathf.RoundToInt(bounds.center.y),
            0
        );

        Vector3 centerWorld = tilemap.CellToWorld(centerCell) + new Vector3(0.5f, 0.5f, 0);

        // Position camera
        transform.position = new Vector3(centerWorld.x, centerWorld.y, -10f);

        // Calculate ortho size based on bounds (prioritize width for landscape maps)
        float worldHeight = bounds.size.y;
        float worldWidth = bounds.size.x;
        float orthoSize = Mathf.Max(worldHeight, worldWidth / cam.aspect) / 2f * padding;
        orthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);

        cam.orthographicSize = orthoSize;
        cam.orthographic = true;

        Debug.Log($"Camera fitted: Center {transform.position}, Ortho Size {orthoSize:F1}");
    }

    private void CenterOnMapSize()
    {
        float centerX = (mapWidth - 1f) * 0.5f;
        float centerY = (mapHeight - 1f) * 0.5f;
        transform.position = new Vector3(centerX, centerY, -10f);

        float orthoSize = Mathf.Max(mapHeight, mapWidth / cam.aspect) * 0.5f * padding;
        cam.orthographicSize = orthoSize;
        cam.orthographic = true;

        Debug.Log($"Fallback camera fit: {mapWidth}x{mapHeight}, Size {orthoSize:F1}");
    }

    // Keyboard controls for manual panning/zoom (WASD + mouse wheel)
    void Update()
    {
        if (Input.GetKey(KeyCode.W)) transform.Translate(Vector3.up * 10f * Time.deltaTime);
        if (Input.GetKey(KeyCode.S)) transform.Translate(Vector3.down * 10f * Time.deltaTime);
        if (Input.GetKey(KeyCode.A)) transform.Translate(Vector3.left * 10f * Time.deltaTime);
        if (Input.GetKey(KeyCode.D)) transform.Translate(Vector3.right * 10f * Time.deltaTime);

        cam.orthographicSize -= Input.mouseScrollDelta.y * 2f * cam.orthographicSize * Time.deltaTime * 5f;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthoSize, maxOrthoSize);
    }
}