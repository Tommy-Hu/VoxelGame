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
    /// <summary>
    /// Holds the chunks that needs to be regenerated either because of placing or breaking of blocks
    /// </summary>
    private HashSet<Vector2Int> chunksPendingRegeneration = new HashSet<Vector2Int>();
    private bool isGeneratedDataLocked = false;

    private Vector2Int FollowTargetPos
    {
        get => Vector2Int.RoundToInt(new Vector2(followTarget.position.x, followTarget.position.z) / ChunkGenerator.CELL_SIZE
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
                Vector3 chunkWorldPos = new Vector3(chunkPos.x * ChunkGenerator.CELL_SIZE * ChunkGenerator.CHUNK_SIZE
, 0, chunkPos.y * ChunkGenerator.CELL_SIZE * ChunkGenerator.CHUNK_SIZE);
                GameObject obj = Instantiate(chunkPrefab, chunkWorldPos, Quaternion.identity, transform);
                obj.name = $"Chunk {chunkPos}";
                chunks.Add(chunkPos, (obj.AddComponent<MeshFilter>(), null));

                ChunkGenerator.AddChunkToPending(chunkPos);
            }
            else if (chunksPendingRegeneration.Contains(chunkPos))
            {
                ChunkGenerator.AddChunkToPendingRegeneration(chunkPos, chunks[chunkPos].Item2);
                chunksPendingRegeneration.Remove(chunkPos);
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
        if (meshFilter.sharedMesh != null) Destroy(meshFilter.sharedMesh);
        meshFilter.sharedMesh = mesh;
        if (meshCollider.sharedMesh != null) Destroy(meshCollider.sharedMesh);
        meshCollider.sharedMesh = mesh;
        Texture2D texture = blocksAtlas;
        if (renderer.material != null) Destroy(renderer.material);
        Material temp = new Material(renderer.material);
        temp.mainTexture = texture;
        renderer.material = temp;
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

    public static Vector2Int BlockWorldPosToBlockChunkPos(Vector2Int worldPos)
    {
        return new Vector2Int(
            ((ChunkGenerator.CHUNK_SIZE + (worldPos.x % ChunkGenerator.CHUNK_SIZE)) % ChunkGenerator.CHUNK_SIZE),
            ((ChunkGenerator.CHUNK_SIZE + (worldPos.y % ChunkGenerator.CHUNK_SIZE)) % ChunkGenerator.CHUNK_SIZE));
    }

    public void SetBlock(int x, int y, int z, string blockType)
    {
        bool px; bool nx; bool pz; bool nz;
        Vector2Int chunkPos = GetChunkOfBlock(x, y, z, out px, out nx, out pz, out nz);

        //add one because (left, bottom, back) block in a chunk starts at (1, 1, 1).
        Vector2Int blockChunkPos = BlockWorldPosToBlockChunkPos(new Vector2Int(x, z)) + new Vector2Int(1, 1);
        x = blockChunkPos.x;
        z = blockChunkPos.y;
        y++;//because y in chunk position starts from 1
        #region set selected block
        {
            Block block = null;
            if (blockType != null)
                block = new Block(blockType, new Vector3Int(x - 1, y - 1, z - 1));//because block starts from (0, 0, 0)
            chunks[chunkPos].Item2.blocks[x, y, z] = block;
            chunksPendingRegeneration.Add(chunkPos);
        }
        #endregion
        #region set neighboring chunk's blocks
        if (px)
        {
            Vector2Int newChunkPos = new Vector2Int(chunkPos.x + 1, chunkPos.y);//the next chunk's x coord is chunkPos.x + 1
            Block block = null;
            if (blockType != null)
                block = new Block(blockType, new Vector3Int(0 - 1, y - 1, z - 1));
            //the next chunk's cache of this block is at x = 0.
            chunks[newChunkPos].Item2.blocks[0, y, z] = block;
            chunksPendingRegeneration.Add(newChunkPos);
        }
        if (nx)
        {
            Vector2Int newChunkPos = new Vector2Int(chunkPos.x - 1, chunkPos.y);
            Block block = null;
            if (blockType != null)
                block = new Block(blockType, new Vector3Int(ChunkGenerator.CHUNK_SIZE + 1, y, z));
            //the next chunk's cache of this block is at x = CHUNK_SIZE + 1
            chunks[newChunkPos].Item2.blocks[ChunkGenerator.CHUNK_SIZE + 1, y, z] = block;
            chunksPendingRegeneration.Add(newChunkPos);
        }
        if (pz)
        {
            Vector2Int newChunkPos = new Vector2Int(chunkPos.x, chunkPos.y + 1);
            Block block = null;
            if (blockType != null)
                block = new Block(blockType, new Vector3Int(x, y, 0));
            //the next chunk's cache of this block is at z = 0
            chunks[newChunkPos].Item2.blocks[x, y, 0] = block;
            chunksPendingRegeneration.Add(newChunkPos);
        }
        if (nz)
        {
            Vector2Int newChunkPos = new Vector2Int(chunkPos.x, chunkPos.y - 1);
            Block block = null;
            if (blockType != null)
                block = new Block(blockType, new Vector3Int(x, y, ChunkGenerator.CHUNK_SIZE + 1));
            //the next chunk's cache of this block is at z = CHUNK_SIZE + 1
            chunks[newChunkPos].Item2.blocks[x, y, ChunkGenerator.CHUNK_SIZE + 1] = block;
            chunksPendingRegeneration.Add(newChunkPos);
        }
        #endregion
    }

    public void RemoveBlock(int x, int y, int z)
    {
        SetBlock(x, y, z, null);
    }

    /// <summary>
    /// Returns the world block pos of the raycasted block. 
    /// The position returned will NOT be offsetted by (1, 1, 1).
    /// </summary>
    /// <param name="normal"></param>
    /// <returns></returns>
    public Vector3Int? GetRaycastedBlock(out Vector3 normal)
    {
        bool isPointingAtBlock = Physics.Raycast(Camera.main.ScreenPointToRay(
            new Vector2(Camera.main.pixelWidth / 2, Camera.main.pixelHeight / 2)
            ), out RaycastHit info, 4f);
        normal = Vector3.zero;
        if (!isPointingAtBlock) return null;
        Vector3 hitPoint = info.point;
        normal = info.normal;
        Vector3Int result;
        print(hitPoint);
        if (normal.x > 0)
        {
            //px face
            result = new Vector3Int((int)Mathf.Floor(hitPoint.x / ChunkGenerator.CELL_SIZE - ChunkGenerator.H_CELL_SIZE),
            (int)Mathf.Floor(hitPoint.y / ChunkGenerator.CELL_SIZE),
            (int)Mathf.Floor(hitPoint.z / ChunkGenerator.CELL_SIZE));
        }
        else if (normal.x < 0)
        {
            //nx face
            result = new Vector3Int((int)Mathf.Floor(hitPoint.x / ChunkGenerator.CELL_SIZE + ChunkGenerator.H_CELL_SIZE),
            (int)Mathf.Floor(hitPoint.y / ChunkGenerator.CELL_SIZE),
            (int)Mathf.Floor(hitPoint.z / ChunkGenerator.CELL_SIZE));
        }
        else if (normal.y > 0)
        {
            //top face
            result = new Vector3Int((int)Mathf.Floor(hitPoint.x / ChunkGenerator.CELL_SIZE),
            (int)Mathf.Floor(hitPoint.y / ChunkGenerator.CELL_SIZE - ChunkGenerator.H_CELL_SIZE),
            (int)Mathf.Floor(hitPoint.z / ChunkGenerator.CELL_SIZE));
        }
        else if (normal.y < 0)
        {
            //bottom face
            result = new Vector3Int((int)Mathf.Floor(hitPoint.x / ChunkGenerator.CELL_SIZE),
            (int)Mathf.Floor(hitPoint.y / ChunkGenerator.CELL_SIZE + ChunkGenerator.H_CELL_SIZE),
            (int)Mathf.Floor(hitPoint.z / ChunkGenerator.CELL_SIZE));
        }
        else if (normal.z > 0)
        {
            //pz face
            result = new Vector3Int((int)Mathf.Floor(hitPoint.x / ChunkGenerator.CELL_SIZE),
            (int)Mathf.Floor(hitPoint.y / ChunkGenerator.CELL_SIZE),
            (int)Mathf.Floor(hitPoint.z / ChunkGenerator.CELL_SIZE - ChunkGenerator.H_CELL_SIZE));
        }
        else
        {
            //nz face
            result = new Vector3Int((int)Mathf.Floor(hitPoint.x / ChunkGenerator.CELL_SIZE),
            (int)Mathf.Floor(hitPoint.y / ChunkGenerator.CELL_SIZE),
            (int)Mathf.Floor(hitPoint.z / ChunkGenerator.CELL_SIZE + ChunkGenerator.H_CELL_SIZE));
        }
        print($"Hits {hitPoint}. Block pos is {result}");
        return result;
    }

    public Vector2Int GetChunkOfBlock(int x, int y, int z,
        out bool isOnChunkPXBorder, out bool isOnChunkNXBorder, out bool isOnChunkPZBorder, out bool isOnChunkNZBorder)
    {
        Vector2Int chunk = new Vector2Int((int)Mathf.Floor(x / ChunkGenerator.CELL_SIZE / ChunkGenerator.CHUNK_SIZE),
            (int)Mathf.Floor(z / ChunkGenerator.CELL_SIZE / ChunkGenerator.CHUNK_SIZE));
        //formula that converts world block pos to chunk block pos is: (16 + (x % 16)) % 16
        Vector2Int blockPosInChunk = BlockWorldPosToBlockChunkPos(new Vector2Int(x, z));
        isOnChunkPXBorder = blockPosInChunk.x >= ChunkGenerator.CHUNK_SIZE - 1;
        isOnChunkNXBorder = blockPosInChunk.x <= 0;
        isOnChunkPZBorder = blockPosInChunk.y >= ChunkGenerator.CHUNK_SIZE - 1;
        isOnChunkNZBorder = blockPosInChunk.y <= 0;
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
