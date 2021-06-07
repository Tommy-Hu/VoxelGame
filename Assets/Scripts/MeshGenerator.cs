using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour
{
    [Range(0, 2)]
    public float scale = 1f;
    public float scaleY = 20f;
    [Range(0, 1)]
    public float setBlockThreshold = 0.2f;
    public int radius = 5;
    public GameObject chunkPrefab;
    public AnimationCurve blockPlacementCurve;
    public int seed;
    public Transform followTarget;
    public int caveGenerationMin = 4;
    public int caveGenerationMax = 128;
    public int surfaceGenerationMin = 120;
    public int surfaceGenerationMax = 133;
    public Texture2D blocksAtlas;

    public string[] blockDataKeys;
    public BlockData[] blockDatas;

    [SerializeField]
    private Dictionary<Vector2Int, (MeshFilter, Chunk)> chunks = new Dictionary<Vector2Int, (MeshFilter, Chunk)>();
    private const int chunkInstantiationsPerFrame = 16;
    private Dictionary<Vector2Int, (MeshData, Chunk)> generatedData = new Dictionary<Vector2Int, (MeshData, Chunk)>();
    private bool isGeneratedDataLocked = false;

    private Vector2Int FollowTargetPos
    {
        get => Vector2Int.RoundToInt(new Vector2(followTarget.position.x, followTarget.position.z) / ChunkGenerator.cellSize
            / ChunkGenerator.CHUNK_SIZE);
    }

    public void DestroyChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        chunks = null;
    }

    private void Start()
    {
        chunks = null;
        DestroyChildren();
        Block.blocksMap = null;
        Random.InitState(seed);
        Vector2 seedOffset = new Vector2(Random.Range(-10000, 10000), Random.Range(-10000, 10000));
        ChunkGenerator.Init(scale, setBlockThreshold, caveGenerationMin, caveGenerationMax, surfaceGenerationMin, surfaceGenerationMax, seedOffset);
        ChunkGenerator.OnDataGenerated = ReceiveMeshData;
        ChunkGenerator.StartThreads();
    }

    private void Update()
    {
        UpdateAll();
    }

    public void UpdateAll()
    {
        if (chunks == null)
            chunks = new Dictionary<Vector2Int, (MeshFilter, Chunk)>();
        if (Block.blocksMap == null)
        {
            Block.blocksMap = new Dictionary<string, BlockData>();
            for (int i = 0; i < blockDataKeys.Length; i++)
            {
                blockDatas[i].RecalculateUVs();
                Block.blocksMap.Add(blockDataKeys[i], blockDatas[i]);
            }
        }
        Vector2Int followTargetPos = FollowTargetPos;
        UpdateChunks(followTargetPos);
    }

    public void UpdateChunks(Vector2Int followTargetPos)
    {
        List<Vector2Int> toDestroy = new List<Vector2Int>();
        foreach (var chunk in chunks)
        {
            if (Mathf.Abs(chunk.Key.x - followTargetPos.x) > radius || Mathf.Abs(chunk.Key.y - followTargetPos.y) > radius)
            {
                toDestroy.Add(chunk.Key);
            }
        }
        for (int i = 0; i < toDestroy.Count; i++)
        {
            chunks[toDestroy[i]].Item1.gameObject.SetActive(false);
        }
        toDestroy.Clear();

        Util.IterateCircular(0, 0, radius, (x, y) =>
        {
            Vector2Int chunkPos = new Vector2Int(x + followTargetPos.x, y + followTargetPos.y);
            bool isChunkGenerated = chunks.ContainsKey(chunkPos);
            if (!isChunkGenerated)
            {
                Vector3 chunkWorldPos = new Vector3(chunkPos.x * ChunkGenerator.cellSize * ChunkGenerator.CHUNK_SIZE
, 0, chunkPos.y * ChunkGenerator.cellSize * ChunkGenerator.CHUNK_SIZE);
                GameObject obj = Instantiate(chunkPrefab, chunkWorldPos, Quaternion.identity, transform);
                obj.name = $"Chunk {chunkPos}";
                chunks.Add(chunkPos, (obj.AddComponent<MeshFilter>(), null));

                ChunkGenerator.AddChunkToPending(chunkPos);
            }
            else
            {
                chunks[chunkPos].Item1.gameObject.SetActive(true);
            }
        });
        while (isGeneratedDataLocked) continue;
        isGeneratedDataLocked = true;
        var clonedGeneratedData = new Dictionary<Vector2Int, (MeshData, Chunk)>(generatedData);
        generatedData.Clear();
        isGeneratedDataLocked = false;
        foreach (var entry in clonedGeneratedData)
        {
            if (!chunks.ContainsKey(entry.Key)) continue;
            MeshFilter chunkFilter = chunks[entry.Key].Item1;
            chunks[entry.Key] = (chunkFilter, entry.Value.Item2);
            DisplayChunk(chunkFilter.GetComponent<MeshRenderer>(), chunkFilter, chunkFilter.GetComponent<MeshCollider>(), entry.Value.Item1);
        }
        ChunkGenerator.AllowGenerating();
    }

    private void ReceiveMeshData(Vector2Int chunkPos, MeshData meshData, Chunk chunk)
    {
        while (isGeneratedDataLocked) continue;
        isGeneratedDataLocked = true;
        if (chunks.ContainsKey(chunkPos))
        {
            if (generatedData.ContainsKey(chunkPos))
                generatedData.Remove(chunkPos);
            generatedData.Add(chunkPos, (meshData, chunk));
        }
        isGeneratedDataLocked = false;
    }

    public void DisplayChunk(MeshRenderer renderer, MeshFilter meshFilter, MeshCollider meshCollider, MeshData meshData)
    {
        if (meshFilter == null || renderer == null) return;

        Mesh mesh = GenerateMesh(meshData);
        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
        Texture2D texture = blocksAtlas;
        Material temp = new Material(renderer.sharedMaterial);
        temp.mainTexture = texture;
        renderer.sharedMaterial = temp;
    }
    public static Mesh GenerateMesh(MeshData meshData)
    {
        Mesh mesh = new Mesh();
        mesh.vertices = meshData.vertices;
        mesh.triangles = meshData.triangles;
        mesh.uv = meshData.uvs;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        return mesh;
    }
    public void RemoveBlock(int x, int y, int z)
    {
        chunks[GetChunkOfBlock(x, y, z, out bool px, out bool nx, out bool pz, out bool nz)].Item2.blocks[x + 1, y + 1, z + 1] = null;
    }

    public Vector3Int? GetRaycastedBlock()
    {
        bool isPointingAtBlock = Physics.Raycast(Camera.main.ScreenPointToRay(
            new Vector2(Camera.main.pixelWidth / 2, Camera.main.pixelHeight / 2)
            ), out RaycastHit info);
        if (!isPointingAtBlock) return null;
        Vector3 hitPoint = info.point;
        return new Vector3Int((int)Mathf.Round(hitPoint.x / ChunkGenerator.cellSize),
            (int)Mathf.Round(hitPoint.y / ChunkGenerator.cellSize),
            (int)Mathf.Round(hitPoint.z / ChunkGenerator.cellSize));
    }

    public Vector2Int GetChunkOfBlock(int x, int y, int z,
        out bool isOnChunkPXBorder, out bool isOnChunkNXBorder, out bool isOnChunkPZBorder, out bool isOnChunkNZBorder)
    {
        Vector2Int chunk = new Vector2Int(x / ChunkGenerator.CHUNK_SIZE, z / ChunkGenerator.CHUNK_SIZE);
        isOnChunkPXBorder = false;
        isOnChunkNXBorder = false;
        isOnChunkPZBorder = false;
        isOnChunkNZBorder = false;
        return chunk;
    }
}

public class MeshData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;
    public int nextTriangleInd;
}
