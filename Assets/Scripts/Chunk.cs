using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Chunk
{
    public readonly Block[,,] blocks = new Block[ChunkGenerator.CHUNK_SIZE + 2, ChunkGenerator.CHUNK_HEIGHT + 2, ChunkGenerator.CHUNK_SIZE + 2];
}
