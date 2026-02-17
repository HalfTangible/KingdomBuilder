using UnityEngine;

[System.Serializable]
public struct TerrainData
{
    public char type;           // '~', ',', 'T', etc. (for quick ref)
    public float travelWeight;  // Path cost (1.0 = easy, 5.0 = hard, Mathf.Infinity = impassable)
    public ResourceType resource;
    public float resourceAmount; // 0-10, randomized per node
    public bool isTown;         // For your @ logic

    // Constructor for defaults
    public TerrainData(char t, float w, ResourceType r = ResourceType.None, float amt = 0f)
    {
        type = t;
        travelWeight = w;
        resource = r;
        resourceAmount = amt;
        isTown = false;
    }
}

public enum ResourceType
{
    None,
    Iron,      // Mountains/hills
    Wood,      // Forests
    Stone,     // Hills/mountains
    Food,      // Plains
    Gold,      // Rare, anywhere
    Fish       // Rivers/coast
}