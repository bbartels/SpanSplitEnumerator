using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SpanSplit
{
    public ref struct BufferedSplitEnumerator2
    {
        private readonly ReadOnlySpan<char> _buffer;
        private readonly Span<int> _buff;
        private readonly Span<int> _buffPrev;
        private readonly char _separator;
        private int _startNext;
        private int _current;
        private int _total;
        private int _bufferLen;

        public BufferedSplitEnumerator2 GetEnumerator() => this;

        public Range Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { 
                ref int ci = ref Unsafe.Add(ref _buffPrev[0], (IntPtr)(uint) _current);
                ref byte c0 = ref Unsafe.As<int, byte>(ref ci);
                var t = Unsafe.ReadUnaligned<long>(ref c0);
                return new Range((int)(t & 0xFFFFFFFF) + 1, (int)(t >> 32)); 
            }
        }

        public BufferedSplitEnumerator2(ReadOnlySpan<char> span, char separator, Span<int> buffer)
        {
            _buffer = span;
            _separator = separator;
            _startNext = 0;
            _buff = buffer.Slice(1);
            _current = -1;
            _total = 0;
            _buffPrev = buffer;
            _buffPrev[0] = -1;
            _buffPrev[_buffer.Length] = span.Length;
            _bufferLen = _buffer.Length;
            PopulateIndices();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int current = _current + 1;

            if (current > _total && _startNext >= _bufferLen || _buffPrev == default) {
                return false;
            }

            if (current > _total) { 
                PopulateIndices();
            }

            _current = current;

            return true;
        }

        // Problems:
        // Lack of Generic type, vector size, struct size
        // Lazy vs greedy iteration
        private void PopulateIndices()
        {
            int startNext = _startNext;
            ReadOnlySpan<char> slice = _buffer.Slice(startNext);

            (int buffCount, int charIndex) = MakeSeparatorListVectorized(slice, _separator, _buff.Slice(0, _buff.Length -1 ), startNext);
            _startNext = charIndex + startNext + 1;
            _buff[buffCount] = _bufferLen;
            _total += buffCount;
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

                //xetract idx
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