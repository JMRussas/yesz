//  YesZ - glTF Accessor Reader
//
//  Resolves glTF accessor indices to typed spans of the BIN chunk data.
//  Handles the accessor → bufferView → buffer chain, byte offsets, and
//  stride-based element-by-element copy for interleaved data.
//
//  Depends on: YesZ.Gltf (GltfDocument, GltfAccessor, GltfBufferView),
//              System.Runtime.InteropServices, System.Runtime.CompilerServices
//  Used by:    MeshExtractor

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YesZ.Gltf;

public class AccessorReader
{
    private readonly GltfDocument _doc;
    private readonly byte[] _binChunk;

    public AccessorReader(GltfDocument doc, byte[] binChunk)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        _binChunk = binChunk ?? throw new ArgumentNullException(nameof(binChunk));
    }

    /// <summary>
    /// Read accessor data as a typed array. Handles both tightly-packed and
    /// strided (interleaved) buffer views.
    /// </summary>
    public T[] Read<T>(int accessorIndex) where T : unmanaged
    {
        var accessor = GetAccessor(accessorIndex);
        var view = GetBufferView(accessor.BufferView
                  ?? throw new InvalidOperationException($"Accessor {accessorIndex} has no bufferView."));

        int viewOffset = view.ByteOffset ?? 0;
        int accessorOffset = accessor.ByteOffset ?? 0;
        int startOffset = viewOffset + accessorOffset;
        int elementSize = Unsafe.SizeOf<T>();
        int stride = view.ByteStride ?? elementSize;
        int count = accessor.Count;

        int lastByteNeeded = startOffset + (count - 1) * stride + elementSize;
        if (count > 0 && lastByteNeeded > _binChunk.Length)
            throw new InvalidOperationException(
                $"Accessor {accessorIndex} reads past end of BIN chunk " +
                $"(needs byte {lastByteNeeded}, chunk is {_binChunk.Length} bytes).");

        var result = new T[count];

        if (stride == elementSize)
        {
            // Tightly packed — bulk copy via MemoryMarshal
            var span = MemoryMarshal.Cast<byte, T>(_binChunk.AsSpan(startOffset, count * elementSize));
            span.CopyTo(result);
        }
        else
        {
            // Strided — copy element by element
            for (int i = 0; i < count; i++)
            {
                int offset = startOffset + i * stride;
                result[i] = MemoryMarshal.Read<T>(_binChunk.AsSpan(offset, elementSize));
            }
        }

        return result;
    }

    /// <summary>
    /// Read index accessor data as ushort[]. Handles UNSIGNED_BYTE (5121),
    /// UNSIGNED_SHORT (5123), and UNSIGNED_INT (5125) component types.
    /// Throws if any uint index exceeds 65,535.
    /// </summary>
    public ushort[] ReadIndices(int accessorIndex)
    {
        var accessor = GetAccessor(accessorIndex);

        return accessor.ComponentType switch
        {
            5121 => ReadByteIndices(accessorIndex),    // UNSIGNED_BYTE
            5123 => Read<ushort>(accessorIndex),  // UNSIGNED_SHORT
            5125 => ReadUintIndices(accessor, accessorIndex), // UNSIGNED_INT
            _ => throw new InvalidOperationException(
                $"Unsupported index component type: {accessor.ComponentType}.")
        };
    }

    private ushort[] ReadByteIndices(int accessorIndex)
    {
        var bytes = Read<byte>(accessorIndex);
        var result = new ushort[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
            result[i] = bytes[i];
        return result;
    }

    private ushort[] ReadUintIndices(GltfAccessor accessor, int accessorIndex)
    {
        var uintIndices = Read<uint>(accessorIndex);
        var result = new ushort[uintIndices.Length];
        for (int i = 0; i < uintIndices.Length; i++)
        {
            if (uintIndices[i] > ushort.MaxValue)
                throw new InvalidOperationException(
                    $"Index {i} has value {uintIndices[i]} which exceeds ushort.MaxValue (65535). " +
                    "Large meshes with >65K vertices are not supported.");
            result[i] = (ushort)uintIndices[i];
        }
        return result;
    }

    private GltfAccessor GetAccessor(int index)
    {
        if (_doc.Accessors == null || index < 0 || index >= _doc.Accessors.Length)
            throw new InvalidOperationException($"Accessor index {index} is out of range.");
        return _doc.Accessors[index];
    }

    private GltfBufferView GetBufferView(int index)
    {
        if (_doc.BufferViews == null || index < 0 || index >= _doc.BufferViews.Length)
            throw new InvalidOperationException($"BufferView index {index} is out of range.");
        return _doc.BufferViews[index];
    }
}
