using UnityEngine;
using System.Collections;

/**
    Generates perlin noisemaps from map info
**/
public static class Noise{

    public enum NormalizeMode {Local, Global}

    public static float[,] generateNoiseMap(int mapWidth, int mapHeight, int seed, float scale, int octaves, float persistance, float lacunarity, Vector2 offset, NormalizeMode normalizeMode) {
        float[,] noiseMap = new float[mapWidth,mapHeight];

        System.Random random = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i=0; i < octaves; i++)
        {
            float offsetX = random.Next(-100000, 100000) + offset.x;
            float offsetY = random.Next(-100000, 100000) - offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= persistance;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        for (int y=0; y < mapHeight; y++)
        {
            for (int x=0; x < mapWidth; x++)
            {
                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;

                for(int i=0; i < octaves; i++)
                {
                    float sampleX = ((x - (mapWidth/2) + octaveOffsets[i].x) / scale) * frequency;
                    float sampleY = ((y - (mapHeight/2) + octaveOffsets[i].y) / scale) * frequency;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }

                if(noiseHeight > maxLocalNoiseHeight)
                {
                    maxLocalNoiseHeight = noiseHeight;
                } else if(noiseHeight < minLocalNoiseHeight)
                {
                    minLocalNoiseHeight = noiseHeight;
                }
                noiseMap [x,y] = noiseHeight;
            }
        }

        for (int y=0; y < noiseMap.GetLength(1); y++)
        {
            for (int x=0; x < noiseMap.GetLength(0); x++)
            {
                if(normalizeMode == NormalizeMode.Local)
                {
                    noiseMap [x,y] = Mathf.InverseLerp (minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x,y]);
                }
                else
                {
                    float normalizedHeight = (noiseMap[x,y] + 1)/(2f * maxPossibleHeight / 2);
                    noiseMap [x,y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }

        return noiseMap;
    }
}
