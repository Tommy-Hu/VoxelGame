using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Chunk
{
    /// <summary>
    /// The blocks in this chunk. blocks[1, 1, 1] represents the (0, 0, 0)'th block in world position, 
    /// and blocks[0, 0, 0] is part of the "rim" of this chunk.
    /// </summary>
    public readonly Block[,,] blocks = new Block[ChunkGenerator.CHUNK_SIZE + 2, ChunkGenerator.CHUNK_HEIGHT + 2, ChunkGenerator.CHUNK_SIZE + 2];
}
