using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SpanSplit
{
    public ref struct ChunkSplitEnumerator
    {
        private readonly ReadOnlySpan<char> _buffer;
        private readonly Span<int> _buff;
        private readonly char _separator;
        private readonly bool _isInitialized;
        private int _startNext;
        private int _buffCount;

        public ChunkSplitEnumerator GetEnumerator() => this;

        public ReadOnlySpan<int> Current => _buff.Slice(0, _buffCount);

        public ChunkSplitEnumerator(ReadOnlySpan<char> span, char separator, Span<int> buffer)
        {
            _isInitialized = true;
            _buffer = span;
            _separator = separator;
            _startNext = 0;
            _buffCount = 0;
            _buff = buffer;
        }

        public bool MoveNext()
        {
            int startNext = _startNext;
            if (!_isInitialized || startNext >= _buffer.Length) {
                return false;
            }

            ReadOnlySpan<char> slice = _buffer.Slice(startNext);

            (_buffCount, int charIndex) = MakeSeparatorListVectorized(slice, _separator, _buff, startNext);
            _startNext = charIndex + startNext + 1;

            return true;
        }


        private (int sepCount, int charIdx) MakeSeparatorListVectorized(ReadOnlySpan<char> source, char c, Span<int> buffer, int off)
        {
            Vector128<byte> shuffleConstant = Vector128.Create(0x00, 0x02, 0x04, 0x06, 0x08, 0x0A, 0x0C, 0x0E, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);

            Vector128<ushort> v1 = Vector128.Create(c);

            ref char c0 = ref MemoryMarshal.GetReference(source);
            int cond = source.Length & -Vector128<ushort>.Count;
            int i = 0;
            int offset = 0;

            for (; i < cond; i += Vector128<ushort>.Count)
            {
                Vector128<ushort> charVector = ReadVector(ref c0, i);
                Vector128<ushort> cmp = Sse2.CompareEqual(charVector, v1);

                if (Sse41.TestZ(cmp, cmp)) { continue; }

                Vector128<byte> mask = Sse2.ShiftRightLogical(cmp.AsUInt64(), 4).AsByte();
                mask = Ssse3.Shuffle(mask, shuffleConstant);

                uint lowBits = Sse2.ConvertToUInt32(mask.AsUInt32());
                mask = Sse2.ShiftRightLogical(mask.AsUInt64(), 32).AsByte();
                uint highBits = Sse2.ConvertToUInt32(mask.AsUInt32());

                for (int idx = i; lowBits != 0; idx++)
                {
                    if ((lowBits & 0xF) != 0)
                    {
                        buffer[offset++] = idx + off;
                        if (offset == buffer.Length) {
                            return (offset, idx);
                        }
                    }

                    lowBits >>= 8;
                }

                for (int idx = i + 4; highBits != 0; idx++)
                {
                    if ((highBits & 0xF) != 0)
                    {
                        buffer[offset++] = idx + off;
                        if (offset == buffer.Length) {
                            return (offset, idx);
                        }
                    }

                    highBits >>= 8;
                }
            }

            for (; i < source.Length; i++)
            {
                char curr = Unsafe.Add(ref c0, (IntPtr)(uint)i);
                if (curr == c)
                {
                    buffer[offset++] = i + off;
                    if (offset == buffer.Length) {
                        return (offset, i);
                    }
                }
            }

            return (offset, i);

            static Vector128<ushort> ReadVector(ref char c0, int offset)
            {
                ref char ci = ref Unsafe.Add(ref c0, (IntPtr)(uint)offset);
                ref byte b = ref Unsafe.As<char, byte>(ref ci);
                return Unsafe.ReadUnaligned<Vector128<ushort>>(ref b);
            }
        }
    }
}