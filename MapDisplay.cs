using UnityEngine;
using System.Collections;

/**
    Draws a map/mesh/texture to an object
**/
public class MapDisplay : MonoBehaviour {

    [SerializeField] private Renderer textureRender;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;

    public void drawTexture(Texture2D texture)
    {
        textureRender.sharedMaterial.mainTexture = texture;
        textureRender.transform.localScale = new Vector3(texture.width, 1, texture.height);
    }

    public void drawMesh(MeshData meshData, Texture2D texture)
    {
        meshFilter.sharedMesh = meshData.createMesh();
        meshRenderer.sharedMaterial.mainTexture = texture;
    }
}
