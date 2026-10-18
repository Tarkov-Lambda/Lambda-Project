using System;
using System.Buffers;
using System.Collections.Generic;
using MemoryPack;

public static class WriterPoolManager
{
    private static readonly Stack<EFTWriterClass> _writers = new();
    private static byte[] _readBuffer = new byte[4096];

    public static EFTWriterClass GetWriter()
    {
        if (_writers.Count == 0)
            return new EFTWriterClass();

        EFTWriterClass writer = _writers.Pop();
        writer.Reset();
        return writer;
    }

    public static void ReturnWriter(EFTWriterClass writer) => _writers.Push(writer);

    // Writes what the EFT writer holds as a length prefixed byte blob (wire compatible with WriteUnmanagedArray<byte>).
    public static void WriteBlob<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer,
        EFTWriterClass eftWriter
    )
        where TBufferWriter : class, IBufferWriter<byte>
    {
        ArraySegment<byte> written = eftWriter.ToArraySegment();
        writer.WriteUnmanagedSpan(
            new ReadOnlySpan<byte>(written.Array, written.Offset, written.Count)
        );
    }

    // The returned segment is only valid until the next TryReadBlob.
    public static bool TryReadBlob(ref MemoryPackReader reader, out ArraySegment<byte> blob)
    {
        reader.DangerousReadUnmanagedSpanView<byte>(out bool isNull, out ReadOnlySpan<byte> view);

        if (isNull || view.Length == 0)
        {
            blob = default;
            return false;
        }

        if (_readBuffer.Length < view.Length)
            _readBuffer = new byte[Math.Max(view.Length, _readBuffer.Length * 2)];

        view.CopyTo(_readBuffer);
        blob = new ArraySegment<byte>(_readBuffer, 0, view.Length);
        return true;
    }
}
