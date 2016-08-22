using UnityEngine;
using System.Collections;

/**
    Generates meshes from height map/data and lod data
**/
public static class MeshGenerator {
    public static MeshData generateTerrainMesh(float[,] heightMap, float heightMultiplier, AnimationCurve heightCurve, int levelOfDetail, int highestLOD)
    {
        AnimationCurve animationCurve = new AnimationCurve(heightCurve.keys);

        int meshSimplificationIncrement = levelOfDetail;
        if(levelOfDetail <= 0)
        {
            meshSimplificationIncrement = 1;
        }

        int borderedSize = heightMap.GetLength(0) - 2*(highestLOD) + 2 * (meshSimplificationIncrement);
        int meshSize = borderedSize - 2;
        int meshSizeSimplified = borderedSize - 2 * meshSimplificationIncrement;

        float topLeftX = (meshSize - 1) / -2f;
        float topLeftZ = (meshSize - 1) / 2f;

        int verticesPerLine = (meshSize-1)/meshSimplificationIncrement + 1;

        int lodDifference = highestLOD - meshSimplificationIncrement;

        MeshData meshData = new MeshData(verticesPerLine);
        

        int[,] vertexIndicesMap = new int[borderedSize,borderedSize];
        int meshVertexIndex = 0;
        int borderVertexIndex = -1;

        for (int y=0; y < borderedSize; y+= meshSimplificationIncrement)
        {
            for (int x=0; x < borderedSize; x+= meshSimplificationIncrement)
            {
                bool isBorderVertex = y == 0 || y == borderedSize -1 || x == 0 || x == borderedSize - 1;

                if(isBorderVertex)
                {
                    vertexIndicesMap[x,y] = borderVertexIndex;
                    borderVertexIndex--;
                }
                else
                {
                    vertexIndicesMap[x,y] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }

        for (int y=0; y < borderedSize; y+= meshSimplificationIncrement)
        {
            for (int x=0; x < borderedSize; x+= meshSimplificationIncrement)
            {
                int vertexIndex = vertexIndicesMap[x,y];
                Vector2 percent = new Vector2((x)/(float)(meshSize), (y)/(float)(meshSize));
                Vector2 percentUV = new Vector2((x - meshSimplificationIncrement)/(float)meshSizeSimplified, (y - meshSimplificationIncrement)/(float)meshSizeSimplified);

                int sampleX = x;
                int sampleY = y;
                float height = animationCurve.Evaluate(heightMap[sampleX + lodDifference,sampleY + lodDifference]) * heightMultiplier;

                Vector3 vertexPosition = new Vector3(topLeftX + percent.x * meshSize, height, topLeftZ - percent.y * meshSize);

                meshData.AddVertex(vertexPosition, percentUV, vertexIndex);

                if(x < borderedSize - 1 && y < borderedSize - 1)
                {
                    int a = vertexIndicesMap[x,y];
                    int b = vertexIndicesMap[x + meshSimplificationIncrement,y];
                    int c = vertexIndicesMap[x,y + meshSimplificationIncrement];
                    int d = vertexIndicesMap[x + meshSimplificationIncrement,y + meshSimplificationIncrement];
                    meshData.AddTriangle(a,d,c);
                    meshData.AddTriangle(d,a,b);
                }

                vertexIndex++;
            }
        }

        return meshData;
    }
}

public class MeshData
{
    Vector3[] vertices;
    int[] triangles;
    Vector2[] uvs;

    Vector3[] borderVertices;
    int[] borderTriangles;

    int triangleIndex;
    int borderTriangleIndex;

    public MeshData(int verticesPerLine)
    {
        vertices = new Vector3[verticesPerLine * verticesPerLine];
        triangles = new int[(verticesPerLine-1) * (verticesPerLine-1) * 6];
        uvs = new Vector2[verticesPerLine * verticesPerLine];

        borderVertices = new Vector3[verticesPerLine * 4 + 4];
        borderTriangles = new int[4 * 6 * verticesPerLine];
    }

    public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex)
    {
        // Border Vertex
        if(vertexIndex < 0)
        {
            borderVertices[-vertexIndex-1] = vertexPosition;
        }
        // Mesh Vertex
        else
        {
            vertices[vertexIndex] = vertexPosition;
            uvs[vertexIndex] = uv;
        }
    }

    public void AddTriangle(int a, int b, int c)
    {
        // Border Triangle
        if (a < 0 || b < 0 || c < 0)
        {
            borderTriangles [borderTriangleIndex] = a;
            borderTriangles [borderTriangleIndex + 1] = b;
            borderTriangles [borderTriangleIndex + 2] = c;
            borderTriangleIndex  += 3;
        }
        // Mesh Triangle
        else
        {
            triangles [triangleIndex] = a;
            triangles [triangleIndex + 1] = b;
            triangles [triangleIndex + 2] = c;
            triangleIndex  += 3;
        }
    }

    Vector3[] calculateNormals()
    {
        Vector3[] vertexNormals = new Vector3[vertices.Length];
        int triangleCount = triangles.Length/3;
        for (int i=0; i<triangleCount; i++)
        {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = triangles[normalTriangleIndex];
            int vertexIndexB = triangles[normalTriangleIndex+1];
            int vertexIndexC = triangles[normalTriangleIndex+2];

            Vector3 triangleNormal = surfaceNormalFromIndeces(vertexIndexA, vertexIndexB, vertexIndexC);
            vertexNormals[vertexIndexA] += triangleNormal;
            vertexNormals[vertexIndexB] += triangleNormal;
            vertexNormals[vertexIndexC] += triangleNormal;
        }

        int borderTriangleCount = borderTriangles.Length/3;
        for (int i=0; i<borderTriangleCount; i++)
        {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = borderTriangles[normalTriangleIndex];
            int vertexIndexB = borderTriangles[normalTriangleIndex+1];
            int vertexIndexC = borderTriangles[normalTriangleIndex+2];

            Vector3 triangleNormal = surfaceNormalFromIndeces(vertexIndexA, vertexIndexB, vertexIndexC);
            if (vertexIndexA >= 0)
            {
                vertexNormals[vertexIndexA] += triangleNormal;
            }
            if (vertexIndexB >= 0)
            {
                vertexNormals[vertexIndexB] += triangleNormal;
            }
            if (vertexIndexC >= 0)
            {
                vertexNormals[vertexIndexC] += triangleNormal;
            }
        }


        for (int i = 0; i < vertexNormals.Length; i ++)
        {
            vertexNormals[i].Normalize();
        }

        return vertexNormals;
    }

    Vector3 surfaceNormalFromIndeces(int indexA, int indexB, int indexC)
    {
        Vector3 pointA;
        Vector3 pointB;
        Vector3 pointC;

        if(indexA < 0)
        {
            pointA = borderVertices[-indexA-1];
        }
        else
        {
            pointA = vertices[indexA];
        }
        if(indexB < 0)
        {
            pointB = borderVertices[-indexB-1];
        }
        else
        {
            pointB = vertices[indexB];
        }
        if(indexC < 0)
        {
            pointC = borderVertices[-indexC-1];
        }
        else
        {
            pointC = vertices[indexC];
        }

        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }

    public Mesh createMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.normals = calculateNormals();
        //mesh.RecalculateNormals();
        return mesh;
    }
}
