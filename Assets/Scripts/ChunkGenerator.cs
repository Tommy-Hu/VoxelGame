using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class ChunkGenerator
{
    public const int CHUNK_SIZE = 16;
    public const int CHUNK_HEIGHT = 256;
    public const float cellSize = 1f;
    public const int generationsPerFrame = 2;
    public const int THREADS_COUNT = 2;

    public static float scale;
    public static float setBlockThreshold;
    public static int caveGenerationMin;
    public static int caveGenerationMax;
    public static int surfaceGenerationMin;
    public static int surfaceGenerationMax;
    public static Vector2 seedOffset;

    public static Thread[] generationThreads;

    public static Action<Vector2Int, MeshData> OnDataGenerated;

    private static List<Vector2Int> pendingGeneration;
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
        ChunkGenerator.scale = scale;
        ChunkGenerator.setBlockThreshold = setBlockThreshold;
        ChunkGenerator.caveGenerationMin = caveGenerationMin;
        ChunkGenerator.caveGenerationMax = caveGenerationMax;
        ChunkGenerator.surfaceGenerationMin = surfaceGenerationMin;
        ChunkGenerator.surfaceGenerationMax = surfaceGenerationMax;
        ChunkGenerator.seedOffset = seedOffset;

        waitHandles = new EventWaitHandle[THREADS_COUNT];

        generationThreads = new Thread[THREADS_COUNT];
        pendingGeneration = new List<Vector2Int>();
        pendingLock = new object();
        generatedDataLocks = new object[THREADS_COUNT];
        pendingGeneration = new List<Vector2Int>();
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

    private static void GenerateChunkDataThreaded(int threadInd)
    {
        while (!stopThread)
        {
            waitHandles[threadInd].WaitOne();
            for (int i = 0; i < generationsPerFrame; i++)
            {
                Vector2Int? curKey;
                lock (pendingLock)
                {
                    if (pendingGeneration.Count > 0)
                    {
                        curKey = pendingGeneration[0];
                        pendingGeneration.RemoveAt(0);
                    }
                    else break;
                }
                if (curKey != null)
                {
                    MeshData genDat = GenerateChunkData(curKey.Value);
                    OnDataGenerated.Invoke(curKey.Value, genDat);
                }
            }
            waitHandles[threadInd].Reset();
        }
    }

    private static MeshData GenerateChunkData(Vector2Int chunkPos)
    {
        Chunk chunk = GenerateChunk(new Vector2(
            chunkPos.x * scale * (CHUNK_SIZE - 1),
            chunkPos.y * scale * (CHUNK_SIZE - 1))
            + seedOffset);
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
    public static Chunk GenerateChunk(Vector2 offset)
    {
        Chunk result = new Chunk();
        for (int z = -1; z < CHUNK_SIZE + 1; z++)
        {
            for (int x = -1; x < CHUNK_SIZE + 1; x++)
            {
                float maxSurfaceBlockHeight = GetMaxSurfaceBlockHeight(offset.x + x * scale, offset.y + z * scale);
                for (int y = -1; y < CHUNK_HEIGHT + 1; y++)
                {
                    result.blocks[x + 1, y + 1, z + 1] = GetBlock(offset, x, y, z, maxSurfaceBlockHeight);
                }
            }
        }
        return result;
    }
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
        return CavesThickness + caveGenerationMin + mappedNoise;
    }
    public static Block GetBlock(Vector2 offset, float x, float y, float z, float maxSurfaceBlockHeight)
    {
        Block block = null;//what type of block is this if it is not air?
        bool hasBlock = false;//is this a block or air?

        if (y >= caveGenerationMin && y < caveGenerationMax)
        {
            block = new Block("Stone", new Vector3(x * cellSize, y * cellSize, z * cellSize));
            hasBlock = IsBlockInCaves(offset.x + x * scale, y * scale, offset.y + z * scale);//scale the offsets to generate random noise
        }
        else if (y < caveGenerationMin)
        {
            if (y != -1)
            {
                block = new Block("Bedrock", new Vector3(x * cellSize, y * cellSize, z * cellSize));
                hasBlock = true;
            }
        }
        else if (y >= surfaceGenerationMin && y < surfaceGenerationMax)
        {
            //scale the offsets EXCEPT Y to generate random surface noise
            if (y < caveGenerationMax)
            {
                hasBlock = false;
            }
            else
            {
                hasBlock = IsBlockOnSurface(y, maxSurfaceBlockHeight, out bool isTopMostLayer);
                if (hasBlock)
                {
                    if (isTopMostLayer)
                        block = new Block("Grass", new Vector3(x * cellSize, y * cellSize, z * cellSize));
                    else
                        block = new Block("Dirt", new Vector3(x * cellSize, y * cellSize, z * cellSize));
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
}
