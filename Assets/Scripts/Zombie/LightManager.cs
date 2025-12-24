using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages and caches all lights in the scene for efficient queries.
/// Eliminates expensive FindObjectsByType calls every frame.
/// </summary>
public class LightManager : MonoBehaviour
{
    public static LightManager Instance { get; private set; }

    [Header("Performance Settings")]
    [Tooltip("How often to refresh the light cache (seconds)")]
    public float cacheRefreshInterval = 2f;
    [Tooltip("Maximum distance to consider lights (optimization)")]
    public float maxLightQueryDistance = 50f;

    // Cached lights - separated by type for faster queries
    private List<Light> pointLights = new List<Light>();
    private List<Light> spotLights = new List<Light>();
    private Light directionalLight;

    // Spatial grid for fast nearby queries
    private Dictionary<Vector2Int, List<Light>> lightGrid = new Dictionary<Vector2Int, List<Light>>();
    private const float GRID_CELL_SIZE = 20f;

    private float nextCacheRefresh;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            RefreshLightCache();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (Time.time >= nextCacheRefresh)
        {
            RefreshLightCache();
            nextCacheRefresh = Time.time + cacheRefreshInterval;
        }
    }

    /// <summary>
    /// Rebuild the light cache. Call this if lights are added/removed dynamically.
    /// </summary>
    public void RefreshLightCache()
    {
        pointLights.Clear();
        spotLights.Clear();
        lightGrid.Clear();
        directionalLight = null;

        Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);

        foreach (Light light in allLights)
        {
            if (!light.enabled || light.intensity <= 0) continue;

            switch (light.type)
            {
                case LightType.Point:
                    pointLights.Add(light);
                    AddToGrid(light);
                    break;
                case LightType.Spot:
                    spotLights.Add(light);
                    AddToGrid(light);
                    break;
                case LightType.Directional:
                    if (directionalLight == null || light.intensity > directionalLight.intensity)
                        directionalLight = light;
                    break;
            }
        }
    }

    void AddToGrid(Light light)
    {
        Vector2Int cell = GetGridCell(light.transform.position);

        // Add to this cell and neighboring cells (for lights that span cells)
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Vector2Int neighborCell = new Vector2Int(cell.x + x, cell.y + z);
                if (!lightGrid.ContainsKey(neighborCell))
                    lightGrid[neighborCell] = new List<Light>();

                if (!lightGrid[neighborCell].Contains(light))
                    lightGrid[neighborCell].Add(light);
            }
        }
    }

    Vector2Int GetGridCell(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / GRID_CELL_SIZE),
            Mathf.FloorToInt(position.z / GRID_CELL_SIZE)
        );
    }

    /// <summary>
    /// Get lights near a position using spatial grid (FAST).
    /// </summary>
    public void GetNearbyLights(Vector3 position, List<Light> results, float maxRange = -1)
    {
        results.Clear();

        if (maxRange < 0) maxRange = maxLightQueryDistance;

        Vector2Int cell = GetGridCell(position);

        // Check current cell and neighbors
        int cellRadius = Mathf.CeilToInt(maxRange / GRID_CELL_SIZE);
        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                Vector2Int checkCell = new Vector2Int(cell.x + x, cell.y + z);
                if (lightGrid.TryGetValue(checkCell, out List<Light> cellLights))
                {
                    foreach (Light light in cellLights)
                    {
                        if (light == null || !light.enabled) continue;

                        float dist = Vector3.Distance(position, light.transform.position);
                        if (dist <= maxRange && !results.Contains(light))
                        {
                            results.Add(light);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Get all point lights (cached).
    /// </summary>
    public List<Light> GetPointLights() => pointLights;

    /// <summary>
    /// Get all spot lights (cached).
    /// </summary>
    public List<Light> GetSpotLights() => spotLights;

    /// <summary>
    /// Get the main directional light (cached).
    /// </summary>
    public Light GetDirectionalLight() => directionalLight;

    /// <summary>
    /// Register a new light (call when spawning lights at runtime).
    /// </summary>
    public void RegisterLight(Light light)
    {
        if (light == null || !light.enabled) return;

        switch (light.type)
        {
            case LightType.Point:
                if (!pointLights.Contains(light))
                {
                    pointLights.Add(light);
                    AddToGrid(light);
                }
                break;
            case LightType.Spot:
                if (!spotLights.Contains(light))
                {
                    spotLights.Add(light);
                    AddToGrid(light);
                }
                break;
            case LightType.Directional:
                directionalLight = light;
                break;
        }
    }

    /// <summary>
    /// Unregister a light (call when destroying lights).
    /// </summary>
    public void UnregisterLight(Light light)
    {
        pointLights.Remove(light);
        spotLights.Remove(light);

        // Remove from grid
        foreach (var cellLights in lightGrid.Values)
        {
            cellLights.Remove(light);
        }
    }
}
