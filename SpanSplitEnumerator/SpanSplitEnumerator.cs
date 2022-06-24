using System;


namespace SpanSplit
{
    public static partial class MemoryExtensions
    {
        public static BufferedSplitEnumerator SplitBuff(this ReadOnlySpan<char> span, Span<int> buffer)
            => new BufferedSplitEnumerator(span, ' ', buffer);

        public static BufferedSplitEnumerator SplitBuff(this ReadOnlySpan<char> span, char separator, Span<int> buffer)
            => new BufferedSplitEnumerator(span, separator, buffer);

        public static SpanSplitEnumerator<char> Split(this ReadOnlySpan<char> span)
            => new SpanSplitEnumerator<char>(span, ' ');

        public static SpanSplitEnumerator<char> Split(this ReadOnlySpan<char> span, char separator)
            => new SpanSplitEnumerator<char>(span, separator);

        public static SpanSplitEnumerator<char> Split(this ReadOnlySpan<char> span, string separator)
            => new SpanSplitEnumerator<char>(span, separator ?? string.Empty);
    }
}
 