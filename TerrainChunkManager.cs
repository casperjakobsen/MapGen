using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/**
    Adds and updates chunks of terrain and their colliders
**/

public class TerrainChunkManager : MonoBehaviour {

    const int mapSizeInChunks = 30;
    const float scale = 1;

    const int highestLOD = 6;
    const int colliderDetailLevel = 1;

    const float moveUpdateThreshold = 1f;
    const float sqrMoveUpdateThreshold = moveUpdateThreshold * moveUpdateThreshold;

    const float chunkCollisionDistance = 100f;

    [SerializeField] private LODInfo[] detailLevels;
    public static float maxViewDistance;

    [SerializeField] private Transform viewer;
    [SerializeField] private Material mapMaterial;
    [SerializeField] private PhysicMaterial mapPhysicMaterial;

    public static Vector2 viewerPosition;
    Vector2 viewerPositionLastUpdate;

    private static MapGenerator mapGenerator;
    private int chunkSize;
    private int chunksVisibleInViewDistance;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    //Colliders
    static Queue<TerrainChunk> chunkAddColliderQueue = new Queue<TerrainChunk>();

    void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();

        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;
        chunkSize = MapGenerator.terrainChunkSize-1;
        chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance/chunkSize);

        addAllChunks();
    }

    void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / scale;

        if((viewerPositionLastUpdate - viewerPosition).sqrMagnitude > sqrMoveUpdateThreshold)
        {
            viewerPositionLastUpdate = viewerPosition;
            updateVisibleChunks();
        }

        assignColliders();
    }
    
    void assignColliders()
    {
        if (chunkAddColliderQueue.Count > 0)
        {
            chunkAddColliderQueue.Dequeue().assignCollider();
        }
    }

    void addAllChunks()
    {
         for (int yOffset = 0; yOffset <= mapSizeInChunks-1; yOffset++)
        {
            for (int xOffset = 0; xOffset <= mapSizeInChunks-1; xOffset++)
            {
                Vector2 chunkCoord = new Vector2(xOffset, yOffset);

                terrainChunkDictionary.Add(chunkCoord, new TerrainChunk(chunkCoord, chunkSize, detailLevels, transform, mapMaterial, mapPhysicMaterial));
            }
        }
    }

    void updateVisibleChunks()
    {
        for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)
        {
            terrainChunksVisibleLastUpdate[i].SetVisible(false);
        }
        terrainChunksVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x/chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y/chunkSize);

        for (int yOffset = -chunksVisibleInViewDistance; yOffset <= chunksVisibleInViewDistance; yOffset++)
        {
            for (int xOffset = -chunksVisibleInViewDistance; xOffset <= chunksVisibleInViewDistance; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    TerrainChunk chunk = terrainChunkDictionary[viewedChunkCoord];
                    chunk.UpdateTerrainChunk();
                }
            }
        }
    }

    public class TerrainChunk
    {
        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        LODInfo[] detailLevels;
        LODMesh[]  lodMeshes;

        MapData mapData;
        bool mapDataReceived;
        int prevLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material, PhysicMaterial mapPhysicMaterial)
        {
            this.detailLevels = detailLevels;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer.material = material;

            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshCollider.material = mapPhysicMaterial;
            meshCollider.enabled = false;

            meshObject.transform.position = positionV3 * scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;

            SetVisible(false);

            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i=0; i < detailLevels.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
            }

            mapGenerator.requestMapData(position, highestLOD, onMapDataReceived);
        }

        void onMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;

            Texture2D texture = TextureGenerator.textureFromColorMap(mapData.colorMap, MapGenerator.terrainChunkSize, MapGenerator.terrainChunkSize);
            meshRenderer.material.mainTexture = texture;

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk()
        {
            if(mapDataReceived)
            {
                float viewerDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
                bool visible = viewerDistanceFromNearestEdge <= maxViewDistance;

                if(visible)
                {
                    int lodIndex = 0;

                    for(int i = 0; i < detailLevels.Length - 1; i++)
                    {
                        if(viewerDistanceFromNearestEdge > detailLevels[i].visibleDistanceThreshold)
                        {
                            lodIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Change to correct LOD Mesh
                    if(lodIndex != prevLODIndex)
                    {
                        LODMesh lodMesh = lodMeshes[lodIndex];
                        if(lodMesh.hasMesh)
                        {
                            prevLODIndex = lodIndex;
                            meshFilter.mesh = lodMesh.mesh;
                        }
                        else if(!lodMesh.hasRequestedMesh)
                        {
                            lodMesh.RequestMesh(mapData);
                        }
                    }

                    terrainChunksVisibleLastUpdate.Add(this);
                }

                SetVisible(visible);

                bool canCollide = (viewerDistanceFromNearestEdge <= chunkCollisionDistance);
                if(canCollide)
                {
                    // Set Mesh Collider if not set
                    if(lodMeshes[colliderDetailLevel-1].hasMesh)
                    {
                        if(meshCollider.sharedMesh == null)
                        {
                            chunkAddColliderQueue.Enqueue(this);
                        }
                    }
                    else
                    {
                        //Request LOD mesh for collision detection
                        lodMeshes[colliderDetailLevel-1].RequestMesh(mapData);
                    }
                }

                if(meshCollider.sharedMesh != null)
                {
                    meshCollider.enabled = canCollide;
                }
            }
        }

        public void assignCollider()
        {
            meshCollider.sharedMesh = lodMeshes[colliderDetailLevel-1].mesh;
        }

        public void SetVisible(bool visible)
        {
            //meshObject.SetActive(visible);
            meshRenderer.enabled = visible;
        }

        public bool isVisible()
        {
            return meshObject.activeSelf;
        }
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        System.Action updateCallback;

        public LODMesh(int lod, System.Action updateCallback)
        {
            this.lod = lod;
            this.updateCallback = updateCallback;
            hasMesh = false;
        }

        void onMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.createMesh();
            hasMesh = true;

            updateCallback();
        }

        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            mapGenerator.requestMeshData(mapData, lod, highestLOD, onMeshDataReceived);
        }
    }

    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDistanceThreshold;

    }
}
