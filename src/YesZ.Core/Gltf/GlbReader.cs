//  YesZ - GLB Binary Container Reader
//
//  Parses .glb files (glTF 2.0 binary format) into JSON and BIN chunks.
//  Validates the 12-byte header (magic, version, length) and extracts
//  chunk 0 (JSON) and chunk 1 (BIN).
//
//  Depends on: System
//  Used by:    GltfDocument (deserialization), AccessorReader (BIN data)

using System;
using System.Buffers.Binary;
using System.Text;

namespace YesZ.Gltf;

/// <summary>
/// Result of parsing a .glb file: the JSON string and the raw BIN chunk bytes.
/// </summary>
public readonly record struct GlbData(string Json, byte[] BinChunk);

public static class GlbReader
{
    private const uint GltfMagic = 0x46546C67; // "glTF" in little-endian
    private const uint JsonChunkType = 0x4E4F534A; // "JSON"
    private const uint BinChunkType = 0x004E4942;  // "BIN\0"

    /// <summary>
    /// Parse a .glb byte array into JSON and BIN chunks.
    /// </summary>
    public static GlbData Parse(ReadOnlySpan<byte> data)
    {
        // Header: 12 bytes (magic, version, total length)
        if (data.Length < 12)
            throw new InvalidOperationException("GLB file too short: must be at least 12 bytes.");

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (magic != GltfMagic)
            throw new InvalidOperationException($"Invalid GLB magic: expected 0x{GltfMagic:X8}, got 0x{magic:X8}.");

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4));
        if (version != 2)
            throw new InvalidOperationException($"Unsupported glTF version: {version}. Only version 2 is supported.");

        uint totalLength = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8));
        if (totalLength > data.Length)
            throw new InvalidOperationException($"GLB header claims {totalLength} bytes but file is only {data.Length} bytes.");

        // Chunk 0: JSON
        if (data.Length < 20)
            throw new InvalidOperationException("GLB file too short for JSON chunk header.");

        uint jsonChunkLength = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12));
        uint jsonChunkType = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16));
        if (jsonChunkType != JsonChunkType)
            throw new InvalidOperationException($"First GLB chunk is not JSON: type 0x{jsonChunkType:X8}.");

        int jsonStart = 20;
        int jsonEnd = jsonStart + (int)jsonChunkLength;
        if (jsonEnd > data.Length)
            throw new InvalidOperationException("JSON chunk extends beyond file.");

        string json = Encoding.UTF8.GetString(data.Slice(jsonStart, (int)jsonChunkLength));

        // Chunk 1: BIN (optional — some glTF files are JSON-only)
        byte[] binChunk;
        int binChunkHeaderStart = jsonEnd;
        if (binChunkHeaderStart + 8 <= data.Length)
        {
            uint binChunkLength = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(binChunkHeaderStart));
            uint binChunkTypeVal = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(binChunkHeaderStart + 4));
            if (binChunkTypeVal != BinChunkType)
                throw new InvalidOperationException($"Second GLB chunk is not BIN: type 0x{binChunkTypeVal:X8}.");

            int binStart = binChunkHeaderStart + 8;
            int binEnd = binStart + (int)binChunkLength;
            if (binEnd > data.Length)
                throw new InvalidOperationException("BIN chunk extends beyond file.");

            binChunk = data.Slice(binStart, (int)binChunkLength).ToArray();
        }
        else
        {
            binChunk = Array.Empty<byte>();
        }

        return new GlbData(json, binChunk);
    }
}
