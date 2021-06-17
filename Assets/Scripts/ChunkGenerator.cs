using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class ChunkGenerator
{
    /// <summary>
    /// The L and W of a chunk
    /// </summary>
    public const int CHUNK_SIZE = 16;
    /// <summary>
    /// How many blocks in the y-axis can there be in a chunk?
    /// </summary>
    public const int CHUNK_HEIGHT = 256;
    public const float CELL_SIZE = 1f;
    /// <summary>
    /// Represents the size of half a cell.
    /// </summary>
    public const float H_CELL_SIZE = 0.5f;
    public const int generationsPerFrame = 2;
    public const int THREADS_COUNT = 2;

    public static float noiseScale;
    public static float setBlockThreshold;
    public static int caveGenerationMin;
    public static int caveGenerationMax;
    public static int surfaceGenerationMin;
    public static int surfaceGenerationMax;
    public static Vector2 seedOffset;

    public static Thread[] generationThreads;

    public static Action<Vector2Int, MeshData, Chunk> OnDataGenerated;

    private static List<Vector2Int> pendingGeneration;
    private static Dictionary<Vector2Int, Chunk> pendingChunkRegeneration;
    private static object pendingLock;
    public static object[] generatedDataLocks;
    private static bool stopThread = false;
    private static EventWaitHandle[] waitHandles;

    public static int SurfaceThickness => surfaceGenerationMax - surfaceGenerationMin;
    public static int CavesThickness => caveGenerationMax - caveGenerationMin;

    public static void Init(float scale, float setBlockThreshold,
        int caveGenerationMin, int caveGenerationMax, int surfaceGenerationMin, int surfaceGenerationMax,
        Vector2 seedOffset)
    {
        ChunkGenerator.noiseScale = scale;
        ChunkGenerator.setBlockThreshold = setBlockThreshold;
        ChunkGenerator.caveGenerationMin = caveGenerationMin;
        ChunkGenerator.caveGenerationMax = caveGenerationMax;
        ChunkGenerator.surfaceGenerationMin = surfaceGenerationMin;
        ChunkGenerator.surfaceGenerationMax = surfaceGenerationMax;
        ChunkGenerator.seedOffset = seedOffset;

        waitHandles = new EventWaitHandle[THREADS_COUNT];

        generationThreads = new Thread[THREADS_COUNT];
        pendingLock = new object();
        generatedDataLocks = new object[THREADS_COUNT];
        pendingGeneration = new List<Vector2Int>();
        pendingChunkRegeneration = new Dictionary<Vector2Int, Chunk>();
        for (int i = 0; i < THREADS_COUNT; i++)
        {
            int ind = i;
            generationThreads[i] = new Thread(() =>
            {
                GenerateChunkDataThreaded(ind);
            });
            generationThreads[ind].IsBackground = true;
            generatedDataLocks[ind] = new object();
            waitHandles[ind] = new EventWaitHandle(false, EventResetMode.ManualReset);
        }
    }

    public static async void StartThreads()
    {
        stopThread = false;

        for (int i = 0; i < THREADS_COUNT; i++)
        {
            await Task.Delay(100);
            generationThreads[i].Start();
        }
    }

    public static bool DoneGenerating()
    {
        for (int i = 0; i < THREADS_COUNT; i++)
        {
            if (waitHandles[i].WaitOne(0) == true)
                return false;
        }
        return true;
    }

    public static void AllowGenerating()
    {
        for (int i = 0; i < THREADS_COUNT; i++)
        {
            waitHandles[i].Set();
        }
    }

    public static void DisallowGenerating()
    {
        for (int i = 0; i < waitHandles.Length; i++)
        {
            waitHandles[i].Reset();
        }
    }

    public static void StopThread()
    {
        stopThread = true;
        for (int i = 0; i < THREADS_COUNT; i++)
        {
            generationThreads[i].Join();
        }
    }

    public static void AddChunkToPending(Vector2Int pending)
    {
        lock (pendingLock)
        {
            pendingGeneration.Add(pending);
        }
    }

    public static void AddChunkToPendingRegeneration(Vector2Int pos, Chunk pending)
    {
        lock (pendingLock)
        {
            if (pendingChunkRegeneration.ContainsKey(pos)) pendingChunkRegeneration.Remove(pos);
            pendingChunkRegeneration.Add(pos, pending);
        }
    }

    private static void GenerateChunkDataThreaded(int threadInd)
    {
        while (!stopThread)
        {
            waitHandles[threadInd].WaitOne();
            for (int i = 0; i < generationsPerFrame; i++)
            {
                Vector2Int? curKey = null;
                Chunk chunk = null;
                lock (pendingLock)
                {
                    if (pendingChunkRegeneration.Count > 0)
                    {
                        curKey = pendingChunkRegeneration.First().Key;
                        chunk = pendingChunkRegeneration[curKey.Value];
                        pendingChunkRegeneration.Remove(curKey.Value);
                    }
                    else if (pendingGeneration.Count > 0)
                    {
                        curKey = pendingGeneration[0];
                        pendingGeneration.RemoveAt(0);
                    }
                    else break;
                }
                if (chunk != null)
                {
                    MeshData genDat = GenerateMeshData(chunk);
                    OnDataGenerated.Invoke(curKey.Value, genDat, chunk);
                }
                else if (curKey != null)
                {
                    MeshData genDat = GenerateChunkData(curKey.Value, out chunk);
                    OnDataGenerated.Invoke(curKey.Value, genDat, chunk);
                }
            }
            waitHandles[threadInd].Reset();
        }
    }

    private static MeshData GenerateChunkData(Vector2Int chunkPos, out Chunk chunk)
    {
        chunk = GenerateChunk(chunkPos);
        MeshData meshData = GenerateMeshData(chunk);
        return meshData;
    }
    public static MeshData GenerateMeshData(Chunk chunk)
    {
        MeshData data = new MeshData();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        int maxTriangleVal = 0;
        for (int z = 1; z <= CHUNK_SIZE; z++)
        {
            for (int x = 1; x <= CHUNK_SIZE; x++)
            {
                for (int y = 1; y <= CHUNK_HEIGHT; y++)
                {
                    Block block = chunk.blocks[x, y, z];

                    Block nX = chunk.blocks[x - 1, y, z];
                    Block pX = chunk.blocks[x + 1, y, z];

                    Block nY = chunk.blocks[x, y - 1, z];
                    Block pY = chunk.blocks[x, y + 1, z];

                    Block nZ = chunk.blocks[x, y, z - 1];
                    Block pZ = chunk.blocks[x, y, z + 1];

                    if (block != null && (nX == null || pX == null || nY == null || pY == null || nZ == null || pZ == null))
                    {
                        MeshData cubeData = block.GenerateCubeMeshData(
                            nX != null,
                            pX != null,
                            nY != null,
                            pY != null,
                            nZ != null,
                            pZ != null,
                            maxTriangleVal);

                        maxTriangleVal = cubeData.nextTriangleInd;
                        vertices.AddRange(cubeData.vertices);
                        uvs.AddRange(cubeData.uvs);
                        triangles.AddRange(cubeData.triangles);
                    }
                }
            }
        }

        data.vertices = vertices.ToArray();
        data.triangles = triangles.ToArray();
        data.uvs = uvs.ToArray();
        return data;
    }
    /// <summary>
    /// Generates chunk with a chunkPos.
    /// </summary>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static Chunk GenerateChunk(Vector2Int chunkPos)
    {
        Chunk result = new Chunk();
        for (int z = -1; z < CHUNK_SIZE + 1; z++)
        {
            for (int x = -1; x < CHUNK_SIZE + 1; x++)
            {
                Vector2 blockNoiseWorldPos = new Vector2(
                    (chunkPos.x * CHUNK_SIZE + x) * CELL_SIZE * noiseScale + seedOffset.x,
                    (chunkPos.y * CHUNK_SIZE + z) * CELL_SIZE * noiseScale + seedOffset.y);//represents the offset of this block
                float maxSurfaceBlockHeight = GetMaxSurfaceBlockHeight(blockNoiseWorldPos.x, blockNoiseWorldPos.y);
                for (int y = -1; y < CHUNK_HEIGHT + 1; y++)
                {
                    result.blocks[x + 1, y + 1, z + 1] = GetBlock(
                        blockNoiseWorldPos.x,
                        y * CELL_SIZE * noiseScale,
                        blockNoiseWorldPos.y,
                        x, y, z,
                        maxSurfaceBlockHeight);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// is this block at (x, y, z), which is in a cave, a solid block or air block?
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static bool IsBlockInCaves(float x, float y, float z)
    {
        return Noise3D(x, y, z) >= setBlockThreshold;
    }

    public static bool IsBlockOnSurface(float nonModifiedY, float maxSurfaceBlockHeight, out bool isTopMostLayer)
    {
        bool isBlock = nonModifiedY < maxSurfaceBlockHeight;
        isTopMostLayer = nonModifiedY + 1 >= maxSurfaceBlockHeight;
        return isBlock;
    }

    public static float GetMaxSurfaceBlockHeight(float x, float z)
    {
        float normalizedNoise = Noise2D(x, z);
        float mappedNoise = normalizedNoise * SurfaceThickness;
        return surfaceGenerationMin + mappedNoise;
    }

    /// <summary>
    /// Gets a block at (x, y, z) where x, y, and z are scaled and offsetted by chunk pos, scale, CELL_SIZE, and seedOffset etc.
    /// Where bx, by, bz are block position in chunk pos. (i.e, (0, 0, 0) (NOT (1, 1, 1)!!!!) means origin block of this chunk).
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="maxSurfaceBlockHeight"></param>
    /// <returns></returns>
    public static Block GetBlock(float x, float y, float z, int bx, int by, int bz, float maxSurfaceBlockHeight)
    {
        Block block = null;//what type of block is this if it is not air?
        bool hasBlock = false;//is this a block or air?

        if (by >= caveGenerationMin && by < caveGenerationMax)
        {
            if (IsBlockCoalOre(x, y, z))
                block = new Block("Coal Ore", new Vector3(bx, by, bz));
            else if (IsBlockIronOre(x, y, z))
                block = new Block("Iron Ore", new Vector3(bx, by, bz));
            else if (IsBlockGoldOre(x, y, z))
                block = new Block("Gold Ore", new Vector3(bx, by, bz));
            else if (IsBlockDiamondOre(x, y, z))
                block = new Block("Diamond Ore", new Vector3(bx, by, bz));
            else if (IsBlockGranite(x, y, z))
                block = new Block("Granite", new Vector3(bx, by, bz));
            else if (IsBlockAndesite(x, y, z))
                block = new Block("Andesite", new Vector3(bx, by, bz));
            else if (IsBlockDiorite(x, y, z))
                block = new Block("Diorite", new Vector3(bx, by, bz));
            else
                block = new Block("Stone", new Vector3(bx, by, bz));
            hasBlock = IsBlockInCaves(x, y, z);//scale the offsets to generate random noise
        }
        else if (by < caveGenerationMin)
        {
            if (by != -1)
            {
                block = new Block("Bedrock", new Vector3(bx, by, bz));
                hasBlock = true;
            }
        }
        else if (by >= surfaceGenerationMin && by < surfaceGenerationMax)
        {
            //scale the offsets EXCEPT Y to generate random surface noise
            if (by < caveGenerationMax)
            {
                hasBlock = false;
            }
            else
            {
                hasBlock = IsBlockOnSurface(by, maxSurfaceBlockHeight, out bool isTopMostLayer);
                if (hasBlock)
                {
                    if (isTopMostLayer)
                        block = new Block("Grass", new Vector3(bx, by, bz));
                    else
                        block = new Block("Dirt", new Vector3(bx, by, bz));
                }
            }
        }
        return hasBlock ? block : null;
    }

    public static float Noise3D(float x, float y, float z)
    {
        float ab = Mathf.PerlinNoise(x, y);
        float bc = Mathf.PerlinNoise(y, z);
        float ac = Mathf.PerlinNoise(x, z);

        float ba = Mathf.PerlinNoise(y, x);
        float cb = Mathf.PerlinNoise(z, y);
        float ca = Mathf.PerlinNoise(z, x);

        float abc = ab + bc + ac + ba + cb + ca;
        return abc / 6f;
    }

    public static float Noise2D(float x, float z)
    {
        return Mathf.PerlinNoise(x, z);
    }

    public static bool IsBlockGranite(float x, float y, float z)
    {
        float offsetX = -123;
        float offsetY = -1235;
        float offsetZ = 4231;
        float scale = 1.8f;
        float threshold = 0.35f;
        return Noise3D((x + offsetX) * scale, (y + offsetY) * scale, (z + offsetZ) * scale) <= threshold;
    }

    public static bool IsBlockAndesite(float x, float y, float z)
    {
        float offsetX = 5525;
        float offsetY = 12341;
        float offsetZ = -234;
        float scale = 1.9f;
        float threshold = 0.35f;
        return Noise3D((x + offsetX) * scale, (y + offsetY) * scale, (z + offsetZ) * scale) <= threshold;
    }

    public static bool IsBlockDiorite(float x, float y, float z)
    {
        float offsetX = -13;
        float offsetY = 1234;
        float offsetZ = 5234;
        float scale = 2f;
        float threshold = 0.35f;
        return Noise3D((x + offsetX) * scale, (y + offsetY) * scale, (z + offsetZ) * scale) <= threshold;
    }


    public static bool IsBlockCoalOre(float x, float y, float z)
    {
        float offsetX = -545;
        float offsetY = 124;
        float offsetZ = 23;
        float scale = 3f;
        float threshold = 0.35f;
        return Noise3D((x + offsetX) * scale, (y + offsetY) * scale, (z + offsetZ) * scale) <= threshold;
    }

    public static bool IsBlockIronOre(float x, float y, float z)
    {
        float offsetX = 1002;
        float offsetY = 1010;
        float offsetZ = 1691;
        float scale = 5f;
        float threshold = 0.31f;
        return Noise3D((x + offsetX) * scale, (y + offsetY) * scale, (z + offsetZ) * scale) <= threshold;
    }

    public static bool IsBlockGoldOre(float x, float y, float z)
    {
        float offsetX = 2512;
        float offsetY = -5313;
        float offsetZ = 2143;
        float scale = 8f;
        float threshold = 0.31f;
        return Noise3D((x + offsetX) * scale, (y + offsetY) * scale, (z + offsetZ) * scale) <= threshold;
    }

    public static bool IsBlockDiamondOre(float x, float y, float z)
    {
        float offsetX = 423;
        float offsetY = -512;
        float offsetZ = -691;
        float scale = 9f;
        float threshold = 0.29f;
        //float threshold = 1f;
        return Noise3D((x + offsetX) * scale, (y + offsetY) * scale, (z + offsetZ) * scale) <= threshold;
    }
}
