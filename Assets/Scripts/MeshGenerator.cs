using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour
{
    [Range(0, 2)]
    public float scale = 1f;
    public float scaleY = 20f;
    public float cellSize = 1f;
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
    private Dictionary<Vector2Int, MeshFilter> chunks = new Dictionary<Vector2Int, MeshFilter>();
    private const int chunkInstantiationsPerFrame = 16;
    private Dictionary<Vector2Int, MeshData> generatedData = new Dictionary<Vector2Int, MeshData>();
    private bool isGeneratedDataLocked = false;

    private Vector2Int FollowTargetPos
    {
        get => Vector2Int.RoundToInt(new Vector2(followTarget.position.x, followTarget.position.z) / cellSize / ChunkGenerator.CHUNK_SIZE);
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
            chunks = new Dictionary<Vector2Int, MeshFilter>();
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
            Destroy(chunks[toDestroy[i]].gameObject);
            chunks.Remove(toDestroy[i]);
        }
        toDestroy.Clear();

        Util.IterateCircular(0, 0, radius, (x, y) =>
        {
            Vector2Int chunkPos = new Vector2Int(x + followTargetPos.x, y + followTargetPos.y);
            bool shouldDisplayChunk = !chunks.ContainsKey(chunkPos);
            if (shouldDisplayChunk)
            {
                Vector3 chunkWorldPos = new Vector3(chunkPos.x * cellSize * ChunkGenerator.CHUNK_SIZE
, 0, chunkPos.y * cellSize * ChunkGenerator.CHUNK_SIZE);
                GameObject obj = Instantiate(chunkPrefab, chunkWorldPos, Quaternion.identity, transform);
                obj.name = $"Chunk {chunkPos}";
                chunks.Add(chunkPos, obj.AddComponent<MeshFilter>());

                ChunkGenerator.AddChunkToPending(chunkPos);
            }
        });
        while (isGeneratedDataLocked) continue;
        isGeneratedDataLocked = true;
        print($"Copying {generatedData.Count}");
        var clonedGeneratedData = new Dictionary<Vector2Int, MeshData>(generatedData);
        generatedData.Clear();
        isGeneratedDataLocked = false;
        foreach (var entry in clonedGeneratedData)
        {
            if (!chunks.ContainsKey(entry.Key)) continue;
            MeshFilter chunkFilter = chunks[entry.Key];
            DisplayChunk(chunkFilter.GetComponent<MeshRenderer>(), chunkFilter, chunkFilter.GetComponent<MeshCollider>(), entry.Value);
        }
        ChunkGenerator.AllowGenerating();
    }

    private void ReceiveMeshData(Vector2Int chunkPos, MeshData meshData)
    {
        Debug.LogWarning("Receive");
        while (isGeneratedDataLocked) continue;
        isGeneratedDataLocked = true;
        if (chunks.ContainsKey(chunkPos))
        {
            if (generatedData.ContainsKey(chunkPos))
                generatedData.Remove(chunkPos);
            generatedData.Add(chunkPos, meshData);
        }
        isGeneratedDataLocked = false;
        Debug.LogWarning("Finished Receive");
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
}

public class MeshData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;
    public int nextTriangleInd;
}
