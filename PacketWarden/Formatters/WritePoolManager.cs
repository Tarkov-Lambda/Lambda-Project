using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class WriterPoolManager
{
    private static readonly Stack<EFTWriterClass> _writers;

    static WriterPoolManager()
    {
        _writers = new Stack<EFTWriterClass>(1);
        _writers.Push(new EFTWriterClass());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReturnWriter(EFTWriterClass writer)
    {
        _writers.Push(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EFTWriterClass GetWriter()
    {
        if (_writers.Count == 0)
        {
            return new EFTWriterClass();
        }

        EFTWriterClass eFTWriterClass = _writers.Pop();
        eFTWriterClass.Reset();
        return eFTWriterClass;
    }
}