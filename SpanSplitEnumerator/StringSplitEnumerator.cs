using System;
using System.Runtime.CompilerServices;

namespace SpanSplit
{
    public ref struct SpanSplitEnumerator<T> where T : IEquatable<T>
    {
        private readonly ReadOnlySpan<T> _buffer;

        private readonly ReadOnlySpan<T> _separators;
        private readonly T _separator;

        private readonly int _separatorLength;
        private readonly bool _splitOnSingleToken;

        private readonly bool _isInitialized;

        private int _startCurrent;
        private int _endCurrent;
        private int _startNext;

        public SpanSplitEnumerator<T> GetEnumerator() => this;

        public Range Current => new Range(_startCurrent, _endCurrent);

        internal SpanSplitEnumerator(ReadOnlySpan<T> span, ReadOnlySpan<T> separators)
        {
            _isInitialized = true;
            _buffer = span;
            _separators = separators;
            _separator = default!;
            _splitOnSingleToken = false;
            _separatorLength = _separators.Length != 0 ? _separators.Length : 1;
            _startCurrent = 0;
            _endCurrent = 0;
            _startNext = 0;
        }

        internal SpanSplitEnumerator(ReadOnlySpan<T> span, T separator)
        {
            _isInitialized = true;
            _buffer = span;
            _separator = separator;
            _separators = default;
            _splitOnSingleToken = true;
            _separatorLength = 1;
            _startCurrent = 0;
            _endCurrent = 0;
            _startNext = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int startnext = _startNext;
            if (startnext > _buffer.Length || !_isInitialized)
            {
                return false;
            }

            ReadOnlySpan<T> slice = _buffer.Slice(startnext);
            _startCurrent =startnext;

            int separatorIndex = _splitOnSingleToken ? slice.IndexOf(_separator) : slice.IndexOf(_separators);
            int elementLength = (separatorIndex != -1 ? separatorIndex : slice.Length);

            _endCurrent = startnext + elementLength;
            _startNext = _endCurrent + _separatorLength;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveuNext()
        {
            int startnext = _startNext;
            if (!_isInitialized || startnext > _buffer.Length)
            {
                return false;
            }

            ReadOnlySpan<T> slice = _buffer.Slice(startnext);
            _startCurrent =startnext;

            int separatorIndex = _splitOnSingleToken ? slice.IndexOf(_separator) : slice.IndexOf(_separators);
            int elementLength = (separatorIndex != -1 ? separatorIndex : slice.Length);
            int endCurrent = _startCurrent + elementLength;
            _endCurrent = endCurrent;
            _startNext = endCurrent + _separatorLength;
            return true;
        }
    }
}
