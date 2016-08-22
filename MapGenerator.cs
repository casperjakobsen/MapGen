using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading;

/**
    Generates mapData (noise/color maps)
**/

public class MapGenerator : MonoBehaviour {
    public enum DrawMode {Noise, Color, Mesh}
    [SerializeField] private DrawMode drawMode;

    [SerializeField] private Noise.NormalizeMode normalizeMode;

    public const int terrainChunkSize = 49; //Vertices, faces + 1
    [Range(0,6)]
    [SerializeField] private int editorPreviewLOD;
    [SerializeField] private float noiseScale;

    [SerializeField] private int octaves;
    [Range(0,1)]
    [SerializeField] private float persistance;
    [SerializeField] private float lacunarity;

    [SerializeField] private int seed;
    [SerializeField] private Vector2 offset;

    [SerializeField] private float meshHeightMultiplier;
    [SerializeField] private AnimationCurve meshHeightCurve;

    public bool autoUpdate;

    [SerializeField] private TerrainType[] regions;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    public void drawMapInEditor()
    {
        MapData mapData = generateMapData(Vector2.zero, editorPreviewLOD);
        MapDisplay display = GetComponent<MapDisplay>();

        switch(drawMode)
        {
            case DrawMode.Noise:
                display.drawTexture(TextureGenerator.textureFromHeightMap(mapData.heightMap));
                break;
            case DrawMode.Color:
                display.drawTexture(TextureGenerator.textureFromColorMap(mapData.colorMap, terrainChunkSize, terrainChunkSize));
                break;
            case DrawMode.Mesh:
                display.drawMesh(MeshGenerator.generateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, editorPreviewLOD, editorPreviewLOD), 
                    TextureGenerator.textureFromColorMap(mapData.colorMap, terrainChunkSize, terrainChunkSize));
                break;
        }
    }

    public void requestMapData(Vector2 center, int highestLOD, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            mapDataThread(center, highestLOD, callback);
        };

        new Thread (threadStart).Start();
    }

    void mapDataThread(Vector2 center, int highestLOD, Action<MapData> callback)
    {
        MapData mapData = generateMapData(center, highestLOD);
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void requestMeshData(MapData mapData, int lod, int highestLOD, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            meshDataThread(mapData, lod, highestLOD, callback);
        };

        Thread t = new Thread (threadStart);
        t.IsBackground = true;
        t.Start();
    }

    void meshDataThread(MapData mapData, int lod, int highestLOD, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.generateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, lod, highestLOD);
        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    void Update()
    {
        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i=0; i< mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i=0; i< meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    MapData generateMapData(Vector2 center, int highestLOD)
    {
        float[,] noiseMap = Noise.generateNoiseMap(terrainChunkSize + 2*highestLOD, terrainChunkSize + 2*highestLOD, seed, noiseScale, octaves, persistance, lacunarity, center + offset, normalizeMode);

        Color[] colorMap = new Color[terrainChunkSize * terrainChunkSize];
        for (int y=0; y < terrainChunkSize; y++)
        {
            for (int x=0; x < terrainChunkSize; x++)
            {
                float currentHeight = noiseMap[x + highestLOD,y + highestLOD];
                for (int i=0; i < regions.Length; i++)
                {
                    if(currentHeight >= regions[i].height) {
                        colorMap[y * terrainChunkSize + x] = regions[i].color;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, colorMap);
    }

    void OnValidate()
    {
        if(octaves < 1)
        {
            octaves = 1;
        }
        if(noiseScale < 0.01f)
        {
            noiseScale = 0.01f;
        }
    }

    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo (Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}

public struct MapData
{
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData (float[,] heightMap, Color[] colorMap)
    {
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}