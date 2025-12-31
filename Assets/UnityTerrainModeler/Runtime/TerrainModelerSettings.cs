using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityTerrainModeler.Runtime
{
    public enum BiomeType
    {
        Temperate,
        Tundra,
        Desert,
        Tropical,
        Alpine,
        Mediterranean
    }

    public enum GeologicalType
    {
        Volcanic,
        Sedimentary,
        Granite,
        Karst,
        Canyon,
        Archipelago
    }

    [CreateAssetMenu(menuName = "Unity Terrain Modeler/Settings", fileName = "TerrainModelerSettings")]
    public class TerrainModelerSettings : ScriptableObject
    {
        [Header("Target Terrain")]
        public Terrain targetTerrain;

        [Header("World Dimensions")]
        public int heightmapResolution = 1025;
        public int alphamapResolution = 512;
        public int detailResolution = 1024;
        public int detailResolutionPerPatch = 16;
        public Vector3 terrainSize = new Vector3(2000f, 600f, 2000f);
        public float waterLevel = 0.32f;

        [Header("Generation")]
        public int seed = 12345;
        public bool useFalloff = true;
        public AnimationCurve islandFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        public float falloffStrength = 1.15f;
        public float baseHeight = 0.1f;
        public BiomeType biome = BiomeType.Temperate;
        public GeologicalType geologicalType = GeologicalType.Volcanic;

        [Header("Noise Layers")]
        public List<NoiseLayer> noiseLayers = new List<NoiseLayer>
        {
            new NoiseLayer
            {
                name = "Primary",
                noiseType = NoiseType.Perlin,
                amplitude = 1f,
                frequency = 0.0015f,
                octaves = 4,
                persistence = 0.55f,
                lacunarity = 2f
            },
            new NoiseLayer
            {
                name = "Detail",
                noiseType = NoiseType.Ridged,
                amplitude = 0.25f,
                frequency = 0.01f,
                octaves = 3,
                persistence = 0.45f,
                lacunarity = 2.2f
            }
        };

        [Header("Terrain Layers")]
        public List<TerrainLayerProfile> terrainLayers = new List<TerrainLayerProfile>();

        [Header("Scatter")]
        public List<ScatterProfile> scatterProfiles = new List<ScatterProfile>();

        [Header("Vegetation")]
        public List<TreePrototypeProfile> treePrototypes = new List<TreePrototypeProfile>();
        public List<TreeScatterProfile> treeScatterProfiles = new List<TreeScatterProfile>();
        public List<DetailPrototypeProfile> detailPrototypes = new List<DetailPrototypeProfile>();

        public void ApplyBiomeDefaults()
        {
            switch (biome)
            {
                case BiomeType.Tundra:
                    baseHeight = 0.08f;
                    falloffStrength = 1.4f;
                    break;
                case BiomeType.Desert:
                    baseHeight = 0.05f;
                    falloffStrength = 1.25f;
                    break;
                case BiomeType.Tropical:
                    baseHeight = 0.15f;
                    falloffStrength = 1.1f;
                    break;
                case BiomeType.Alpine:
                    baseHeight = 0.18f;
                    falloffStrength = 1.2f;
                    break;
                case BiomeType.Mediterranean:
                    baseHeight = 0.12f;
                    falloffStrength = 1.15f;
                    break;
                default:
                    baseHeight = 0.1f;
                    falloffStrength = 1.15f;
                    break;
            }
        }

        [Serializable]
        public class NoiseLayer
        {
            public string name;
            public bool enabled = true;
            public NoiseType noiseType = NoiseType.Perlin;
            public float amplitude = 1f;
            public float frequency = 0.0015f;
            public int octaves = 4;
            public float persistence = 0.5f;
            public float lacunarity = 2f;
            public Vector2 offset;
        }

        public enum NoiseType
        {
            Perlin,
            Ridged,
            Billow
        }

        [Serializable]
        public class TerrainLayerProfile
        {
            public string name = "Layer";
            public TerrainLayer terrainLayer;
            public float minHeight = 0f;
            public float maxHeight = 1f;
            public float minSlope = 0f;
            public float maxSlope = 45f;
            public float noiseScale = 0.1f;
            public float noiseStrength = 0.2f;
            public float weight = 1f;
        }

        [Serializable]
        public class ScatterProfile
        {
            public string name = "Scatter";
            public List<GameObject> prefabs = new List<GameObject>();
            public int seedOffset = 0;
            public float density = 0.001f;
            public float minHeight = 0f;
            public float maxHeight = 1f;
            public float minSlope = 0f;
            public float maxSlope = 35f;
            public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
            public bool alignToNormal = true;
            public float maxNormalDeviation = 15f;
        }

        [Serializable]
        public class TreePrototypeProfile
        {
            public string name = "Tree";
            public GameObject prefab;
            public float bendFactor = 0.2f;
        }

        [Serializable]
        public class TreeScatterProfile
        {
            public string name = "Tree Scatter";
            public int treePrototypeIndex;
            public float density = 0.001f;
            public float minHeight = 0f;
            public float maxHeight = 1f;
            public float minSlope = 0f;
            public float maxSlope = 35f;
            public Vector2 scaleRange = new Vector2(0.8f, 1.4f);
            public float randomYaw = 360f;
        }

        [Serializable]
        public class DetailPrototypeProfile
        {
            public string name = "Detail";
            public GameObject prefab;
            public Texture2D texture;
            public float minWidth = 0.5f;
            public float maxWidth = 1.2f;
            public float minHeight = 0.5f;
            public float maxHeight = 1.5f;
            public Color healthyColor = Color.white;
            public Color dryColor = new Color(0.8f, 0.75f, 0.65f);
            public float noiseSpread = 0.5f;
            public float density = 0.35f;
            public float minHeightRatio = 0f;
            public float maxHeightRatio = 1f;
            public float minSlope = 0f;
            public float maxSlope = 35f;
        }
    }
}
