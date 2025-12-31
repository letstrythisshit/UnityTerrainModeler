using System.Collections.Generic;
using UnityEngine;

namespace UnityTerrainModeler.Runtime
{
    [ExecuteAlways]
    public class TerrainModelerGenerator : MonoBehaviour
    {
        public TerrainModelerSettings settings;
        public bool applyBiomeDefaultsOnGenerate = true;
        public string scatterContainerName = "Terrain Modeler Instances";

        public void Generate()
        {
            if (settings == null)
            {
                Debug.LogWarning("Terrain Modeler: Missing settings asset.");
                return;
            }

            if (applyBiomeDefaultsOnGenerate)
            {
                settings.ApplyBiomeDefaults();
            }

            Terrain terrain = settings.targetTerrain;
            if (terrain == null)
            {
                terrain = GetComponent<Terrain>();
            }

            if (terrain == null)
            {
                Debug.LogWarning("Terrain Modeler: No Terrain assigned.");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
            {
                terrainData = new TerrainData();
                terrain.terrainData = terrainData;
            }

            terrainData.heightmapResolution = settings.heightmapResolution;
            terrainData.size = settings.terrainSize;
            terrainData.alphamapResolution = settings.alphamapResolution;
            terrainData.SetDetailResolution(settings.detailResolution, settings.detailResolutionPerPatch);

            float[,] heights = BuildHeightmap(settings, terrainData.heightmapResolution);
            terrainData.SetHeights(0, 0, heights);

            ApplyTerrainLayers(terrainData);
            ApplyTreePrototypes(terrainData);
            ApplyDetailPrototypes(terrainData);
            ScatterTrees(terrainData, terrain.transform.position);
            ScatterPrefabs(terrain, terrainData);

            terrain.Flush();
        }

        private float[,] BuildHeightmap(TerrainModelerSettings modelerSettings, int resolution)
        {
            float[,] heights = new float[resolution, resolution];
            System.Random rng = new System.Random(modelerSettings.seed);
            float offsetX = (float)rng.NextDouble() * 10000f;
            float offsetZ = (float)rng.NextDouble() * 10000f;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (resolution - 1f);
                    float v = z / (resolution - 1f);
                    float height = modelerSettings.baseHeight;

                    foreach (TerrainModelerSettings.NoiseLayer layer in modelerSettings.noiseLayers)
                    {
                        if (!layer.enabled)
                        {
                            continue;
                        }

                        float noiseValue = FractalNoise(
                            (u + layer.offset.x) * modelerSettings.terrainSize.x * layer.frequency + offsetX,
                            (v + layer.offset.y) * modelerSettings.terrainSize.z * layer.frequency + offsetZ,
                            layer);
                        height += noiseValue * layer.amplitude;
                    }

                    if (modelerSettings.useFalloff)
                    {
                        float falloff = EvaluateFalloff(modelerSettings, u, v);
                        height *= falloff;
                    }

                    height = ApplyGeologicalModifiers(modelerSettings, height, u, v);
                    heights[z, x] = Mathf.Clamp01(height);
                }
            }

            return heights;
        }

        private float EvaluateFalloff(TerrainModelerSettings modelerSettings, float u, float v)
        {
            float nx = u * 2f - 1f;
            float nz = v * 2f - 1f;
            float distance = Mathf.Sqrt(nx * nx + nz * nz) / Mathf.Sqrt(2f);
            float falloff = modelerSettings.islandFalloff.Evaluate(distance);
            float shaped = Mathf.Pow(falloff, modelerSettings.falloffStrength);
            return Mathf.Clamp01(shaped);
        }

        private float FractalNoise(float x, float z, TerrainModelerSettings.NoiseLayer layer)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float value = 0f;
            float maxValue = 0f;

            for (int i = 0; i < layer.octaves; i++)
            {
                float sample = Mathf.PerlinNoise(x * frequency, z * frequency);
                sample = TransformNoise(sample, layer.noiseType);
                value += sample * amplitude;
                maxValue += amplitude;
                amplitude *= layer.persistence;
                frequency *= layer.lacunarity;
            }

            if (maxValue > 0f)
            {
                value /= maxValue;
            }

            return value;
        }

        private float ApplyGeologicalModifiers(TerrainModelerSettings modelerSettings, float height, float u, float v)
        {
            switch (modelerSettings.geologicalType)
            {
                case TerrainModelerSettings.GeologicalType.Volcanic:
                    float distance = Mathf.Sqrt(Mathf.Pow(u - 0.5f, 2f) + Mathf.Pow(v - 0.5f, 2f));
                    float peak = Mathf.Clamp01(1f - distance * 1.8f);
                    float crater = Mathf.SmoothStep(0.2f, 0.8f, distance * 2.2f);
                    height += peak * 0.25f;
                    height -= crater * 0.15f;
                    break;
                case TerrainModelerSettings.GeologicalType.Sedimentary:
                    float steps = 8f;
                    height = Mathf.Round(height * steps) / steps;
                    break;
                case TerrainModelerSettings.GeologicalType.Granite:
                    height = Mathf.Pow(height, 0.85f);
                    break;
                case TerrainModelerSettings.GeologicalType.Karst:
                    float sinkholes = Mathf.PerlinNoise(u * 6f, v * 6f);
                    height -= sinkholes * 0.15f;
                    break;
                case TerrainModelerSettings.GeologicalType.Canyon:
                    float canyon = Mathf.PerlinNoise(u * 3f, v * 3f);
                    height = Mathf.Lerp(height, height * canyon, 0.6f);
                    break;
                case TerrainModelerSettings.GeologicalType.Archipelago:
                    float islands = Mathf.PerlinNoise(u * 4f, v * 4f);
                    height *= Mathf.Lerp(0.4f, 1f, islands);
                    break;
            }

            return height;
        }

        private float TransformNoise(float sample, TerrainModelerSettings.NoiseType type)
        {
            switch (type)
            {
                case TerrainModelerSettings.NoiseType.Ridged:
                    return 1f - Mathf.Abs(sample * 2f - 1f);
                case TerrainModelerSettings.NoiseType.Billow:
                    return Mathf.Abs(sample * 2f - 1f);
                default:
                    return sample;
            }
        }

        private void ApplyTerrainLayers(TerrainData terrainData)
        {
            List<TerrainModelerSettings.TerrainLayerProfile> layers = settings.terrainLayers;
            if (layers == null || layers.Count == 0)
            {
                return;
            }

            List<TerrainLayer> terrainLayers = new List<TerrainLayer>();
            List<TerrainModelerSettings.TerrainLayerProfile> validProfiles = new List<TerrainModelerSettings.TerrainLayerProfile>();
            foreach (TerrainModelerSettings.TerrainLayerProfile profile in layers)
            {
                if (profile.terrainLayer != null)
                {
                    terrainLayers.Add(profile.terrainLayer);
                    validProfiles.Add(profile);
                }
            }

            if (terrainLayers.Count == 0)
            {
                return;
            }

            terrainData.terrainLayers = terrainLayers.ToArray();

            int alphamapResolution = terrainData.alphamapResolution;
            float[,,] alphamaps = new float[alphamapResolution, alphamapResolution, terrainLayers.Count];

            for (int y = 0; y < alphamapResolution; y++)
            {
                for (int x = 0; x < alphamapResolution; x++)
                {
                    float u = x / (alphamapResolution - 1f);
                    float v = y / (alphamapResolution - 1f);
                    float height = terrainData.GetInterpolatedHeight(u, v) / terrainData.size.y;
                    float slope = terrainData.GetSteepness(u, v);
                    float totalWeight = 0f;

                    for (int layerIndex = 0; layerIndex < terrainLayers.Count; layerIndex++)
                    {
                        TerrainModelerSettings.TerrainLayerProfile profile = validProfiles[layerIndex];
                        float heightFactor = Mathf.InverseLerp(profile.minHeight, profile.maxHeight, height);
                        float slopeFactor = Mathf.InverseLerp(profile.maxSlope, profile.minSlope, slope);
                        float noise = Mathf.PerlinNoise(u * profile.noiseScale, v * profile.noiseScale);
                        float noiseFactor = Mathf.Lerp(1f - profile.noiseStrength, 1f, noise);
                        float weight = Mathf.Clamp01(heightFactor) * Mathf.Clamp01(slopeFactor) * noiseFactor * profile.weight;

                        alphamaps[y, x, layerIndex] = weight;
                        totalWeight += weight;
                    }

                    if (totalWeight > 0f)
                    {
                        for (int layerIndex = 0; layerIndex < terrainLayers.Count; layerIndex++)
                        {
                            alphamaps[y, x, layerIndex] /= totalWeight;
                        }
                    }
                }
            }

            terrainData.SetAlphamaps(0, 0, alphamaps);
        }

        private void ApplyTreePrototypes(TerrainData terrainData)
        {
            if (settings.treePrototypes == null || settings.treePrototypes.Count == 0)
            {
                return;
            }

            List<TreePrototype> prototypes = new List<TreePrototype>();
            foreach (TerrainModelerSettings.TreePrototypeProfile profile in settings.treePrototypes)
            {
                if (profile.prefab == null)
                {
                    continue;
                }

                prototypes.Add(new TreePrototype
                {
                    prefab = profile.prefab,
                    bendFactor = profile.bendFactor
                });
            }

            terrainData.treePrototypes = prototypes.ToArray();
        }

        private void ApplyDetailPrototypes(TerrainData terrainData)
        {
            if (settings.detailPrototypes == null || settings.detailPrototypes.Count == 0)
            {
                return;
            }

            List<DetailPrototype> prototypes = new List<DetailPrototype>();
            foreach (TerrainModelerSettings.DetailPrototypeProfile profile in settings.detailPrototypes)
            {
                DetailPrototype prototype = new DetailPrototype
                {
                    prototype = profile.prefab,
                    prototypeTexture = profile.texture,
                    minWidth = profile.minWidth,
                    maxWidth = profile.maxWidth,
                    minHeight = profile.minHeight,
                    maxHeight = profile.maxHeight,
                    healthyColor = profile.healthyColor,
                    dryColor = profile.dryColor,
                    noiseSpread = profile.noiseSpread,
                    usePrototypeMesh = profile.prefab != null,
                    renderMode = profile.prefab != null ? DetailRenderMode.VertexLit : DetailRenderMode.Grass
                };

                prototypes.Add(prototype);
            }

            terrainData.detailPrototypes = prototypes.ToArray();
            ScatterDetails(terrainData);
        }

        private void ScatterTrees(TerrainData terrainData, Vector3 terrainPosition)
        {
            if (settings.treeScatterProfiles == null || settings.treeScatterProfiles.Count == 0)
            {
                terrainData.treeInstances = new TreeInstance[0];
                return;
            }

            List<TreeInstance> treeInstances = new List<TreeInstance>();
            int resolution = terrainData.heightmapResolution;
            int area = resolution * resolution;

            foreach (TerrainModelerSettings.TreeScatterProfile profile in settings.treeScatterProfiles)
            {
                if (profile.treePrototypeIndex < 0 || profile.treePrototypeIndex >= terrainData.treePrototypes.Length)
                {
                    continue;
                }

                int targetCount = Mathf.RoundToInt(area * profile.density);
                System.Random rng = new System.Random(settings.seed + profile.treePrototypeIndex * 31);

                for (int i = 0; i < targetCount; i++)
                {
                    float u = (float)rng.NextDouble();
                    float v = (float)rng.NextDouble();
                    float height = terrainData.GetInterpolatedHeight(u, v) / terrainData.size.y;
                    float slope = terrainData.GetSteepness(u, v);

                    if (height < settings.waterLevel)
                    {
                        continue;
                    }

                    if (height < profile.minHeight || height > profile.maxHeight)
                    {
                        continue;
                    }

                    if (slope < profile.minSlope || slope > profile.maxSlope)
                    {
                        continue;
                    }

                    float scale = Mathf.Lerp(profile.scaleRange.x, profile.scaleRange.y, (float)rng.NextDouble());

                    treeInstances.Add(new TreeInstance
                    {
                        position = new Vector3(u, height, v),
                        prototypeIndex = profile.treePrototypeIndex,
                        heightScale = scale,
                        widthScale = scale,
                        color = Color.white,
                        lightmapColor = Color.white,
                        rotation = Mathf.Deg2Rad * profile.randomYaw * (float)rng.NextDouble()
                    });
                }
            }

            terrainData.treeInstances = treeInstances.ToArray();
        }

        private void ScatterDetails(TerrainData terrainData)
        {
            if (settings.detailPrototypes == null || settings.detailPrototypes.Count == 0)
            {
                return;
            }

            int detailResolution = terrainData.detailResolution;
            int detailLayerCount = terrainData.detailPrototypes.Length;

            if (detailLayerCount == 0)
            {
                return;
            }

            terrainData.SetDetailResolution(detailResolution, terrainData.detailResolutionPerPatch);

            for (int layerIndex = 0; layerIndex < detailLayerCount; layerIndex++)
            {
                TerrainModelerSettings.DetailPrototypeProfile profile = settings.detailPrototypes[layerIndex];
                int[,] detailLayer = new int[detailResolution, detailResolution];

                for (int y = 0; y < detailResolution; y++)
                {
                    for (int x = 0; x < detailResolution; x++)
                    {
                        float u = x / (detailResolution - 1f);
                        float v = y / (detailResolution - 1f);
                        float height = terrainData.GetInterpolatedHeight(u, v) / terrainData.size.y;
                        float slope = terrainData.GetSteepness(u, v);

                        if (height < settings.waterLevel)
                        {
                            continue;
                        }

                        if (height < profile.minHeightRatio || height > profile.maxHeightRatio)
                        {
                            continue;
                        }

                        if (slope < profile.minSlope || slope > profile.maxSlope)
                        {
                            continue;
                        }

                        float noise = Mathf.PerlinNoise(u * profile.noiseSpread, v * profile.noiseSpread);
                        if (noise < profile.density)
                        {
                            detailLayer[y, x] = 1;
                        }
                    }
                }

                terrainData.SetDetailLayer(0, 0, layerIndex, detailLayer);
            }
        }

        private void ScatterPrefabs(Terrain terrain, TerrainData terrainData)
        {
            if (settings.scatterProfiles == null || settings.scatterProfiles.Count == 0)
            {
                return;
            }

            Transform scatterRoot = GetOrCreateScatterRoot(terrain.transform);

            foreach (TerrainModelerSettings.ScatterProfile profile in settings.scatterProfiles)
            {
                if (profile.prefabs == null || profile.prefabs.Count == 0)
                {
                    continue;
                }

                int resolution = terrainData.heightmapResolution;
                int area = resolution * resolution;
                int targetCount = Mathf.RoundToInt(area * profile.density);
                System.Random rng = new System.Random(settings.seed + profile.seedOffset);

                for (int i = 0; i < targetCount; i++)
                {
                    float u = (float)rng.NextDouble();
                    float v = (float)rng.NextDouble();
                    float height = terrainData.GetInterpolatedHeight(u, v) / terrainData.size.y;
                    float slope = terrainData.GetSteepness(u, v);

                    if (height < settings.waterLevel)
                    {
                        continue;
                    }

                    if (height < profile.minHeight || height > profile.maxHeight)
                    {
                        continue;
                    }

                    if (slope < profile.minSlope || slope > profile.maxSlope)
                    {
                        continue;
                    }

                    int prefabIndex = rng.Next(profile.prefabs.Count);
                    GameObject prefab = profile.prefabs[prefabIndex];
                    if (prefab == null)
                    {
                        continue;
                    }

                    float worldY = terrainData.GetInterpolatedHeight(u, v) + terrain.transform.position.y;
                    Vector3 position = new Vector3(
                        u * terrainData.size.x,
                        worldY,
                        v * terrainData.size.z) + terrain.transform.position;

                    GameObject instance = Instantiate(prefab, position, Quaternion.identity, scatterRoot);
                    float scale = Mathf.Lerp(profile.scaleRange.x, profile.scaleRange.y, (float)rng.NextDouble());
                    instance.transform.localScale = Vector3.one * scale;

                    if (profile.alignToNormal)
                    {
                        Vector3 normal = terrainData.GetInterpolatedNormal(u, v);
                        Quaternion alignRotation = Quaternion.FromToRotation(Vector3.up, normal);
                        Quaternion yawRotation = Quaternion.AngleAxis((float)rng.NextDouble() * 360f, Vector3.up);
                        instance.transform.rotation = alignRotation * yawRotation;
                    }
                    else
                    {
                        instance.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    }

                }
            }
        }

        private Transform GetOrCreateScatterRoot(Transform terrainRoot)
        {
            Transform existing = terrainRoot.Find(scatterContainerName);
            if (existing != null)
            {
                ClearChildren(existing);
                return existing;
            }

            GameObject root = new GameObject(scatterContainerName);
            root.transform.SetParent(terrainRoot, false);
            return root.transform;
        }

        private void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
