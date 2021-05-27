using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Face
{
    public Vector3[] vertices = new Vector3[4];

    public Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        vertices[0] = a;
        vertices[1] = b;
        vertices[2] = c;
        vertices[3] = d;
    }

    public Vector3[] GetOffsetVertices(Vector3 offset)
    {
        return new Vector3[4] { vertices[0] + offset, vertices[1] + offset, vertices[2] + offset, vertices[3] + offset };
    }

    public int[] GetTriangles(int start)
    {
        return new[] { start, start + 1, start + 2, start + 2, start + 1, start + 3 };
    }
}

public class Block
{
    public static Face pY = new Face(new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 0), new Vector3(1, 1, 0));
    public static Face nY = new Face(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1));
    public static Face nX = new Face(new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, 0));
    public static Face pX = new Face(new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 0), new Vector3(1, 0, 1));
    public static Face nZ = new Face(new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(0, 0, 0), new Vector3(1, 0, 0));
    public static Face pZ = new Face(new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(1, 0, 1), new Vector3(0, 0, 1));

    public string blockType;

    public static Dictionary<string, BlockData> blocksMap = new Dictionary<string, BlockData>();

    private Vector3 offset;

    public Block(string type, Vector3 offset)
    {
        this.blockType = type;
        this.offset = offset;
    }

    /// <summary>
    /// Generates a MeshData instance that contains all vertices, triangles, uvs, and nextTriangleStartValue
    /// </summary>
    /// <param name="nX"></param>
    /// <param name="pX"></param>
    /// <param name="nY"></param>
    /// <param name="pY"></param>
    /// <param name="nZ"></param>
    /// <param name="pZ"></param>
    /// <param name="trianglesStartValue"></param>
    /// <returns></returns>
    public MeshData GenerateCubeMeshData(bool nX, bool pX, bool nY, bool pY, bool nZ, bool pZ,
        int trianglesStartValue = 0)
    {
        MeshData merged = new MeshData();
        if (blocksMap == null) Debug.Log("BlocksMap is null!");
        BlockData blockData = blocksMap[blockType];
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        merged.nextTriangleInd = trianglesStartValue;
        if (!pY)
        {
            vertices.AddRange(Block.pY.GetOffsetVertices(offset));
            triangles.AddRange(Block.pY.GetTriangles(merged.nextTriangleInd));
            merged.nextTriangleInd += 4;
            uvs.AddRange(blockData.PYUV);
        }
        if (!nY)
        {
            vertices.AddRange(Block.nY.GetOffsetVertices(offset));
            triangles.AddRange(Block.nY.GetTriangles(merged.nextTriangleInd));
            merged.nextTriangleInd += 4;
            uvs.AddRange(blockData.NYUV);
        }
        if (!nX)
        {
            vertices.AddRange(Block.nX.GetOffsetVertices(offset));
            triangles.AddRange(Block.nX.GetTriangles(merged.nextTriangleInd));
            merged.nextTriangleInd += 4;
            uvs.AddRange(blockData.NXUV);
        }
        if (!pX)
        {
            vertices.AddRange(Block.pX.GetOffsetVertices(offset));
            triangles.AddRange(Block.pX.GetTriangles(merged.nextTriangleInd));
            merged.nextTriangleInd += 4;
            uvs.AddRange(blockData.PXUV);
        }
        if (!nZ)
        {
            vertices.AddRange(Block.nZ.GetOffsetVertices(offset));
            triangles.AddRange(Block.nZ.GetTriangles(merged.nextTriangleInd));
            merged.nextTriangleInd += 4;
            uvs.AddRange(blockData.NZUV);
        }
        if (!pZ)
        {
            vertices.AddRange(Block.pZ.GetOffsetVertices(offset));
            triangles.AddRange(Block.pZ.GetTriangles(merged.nextTriangleInd));
            merged.nextTriangleInd += 4;
            uvs.AddRange(blockData.PZUV);
        }
        merged.vertices = vertices.ToArray();
        merged.triangles = triangles.ToArray();
        merged.uvs = uvs.ToArray();
        return merged;
    }
}

[System.Serializable]
public class BlockData
{
    #region Sprites
    [SerializeField]
    private Sprite PXFace;
    [SerializeField]
    private Sprite NXFace;
    [SerializeField]
    private Sprite PYFace;
    [SerializeField]
    private Sprite NYFace;
    [SerializeField]
    private Sprite PZFace;
    [SerializeField]
    private Sprite NZFace;
    #endregion
    #region UVs
    [HideInInspector]
    public Vector2[] PYUV;
    [HideInInspector]
    public Vector2[] NYUV;
    [HideInInspector]
    public Vector2[] PXUV;
    [HideInInspector]
    public Vector2[] NXUV;
    [HideInInspector]
    public Vector2[] PZUV;
    [HideInInspector]
    public Vector2[] NZUV;
    #endregion

    public void RecalculateUVs()
    {
        PYUV = PYFace.uv;
        NYUV = NYFace.uv;
        PXUV = PXFace.uv;
        NXUV = NXFace.uv;
        PZUV = PZFace.uv;
        NZUV = NZFace.uv;
    }
}