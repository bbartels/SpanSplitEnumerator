using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;

namespace SpanSplit
{
    [SimpleJob(warmupCount: 5, targetCount: 5)]
    public class StringSplitRunner
    {
        public struct Impl
        {
            public Func<string, int> Value { get; private set; }
            public string Name { get; private set; }

            public Impl(Func<string, int> impl, string name)
                => (Value, Name) = (impl, name);

            public override string ToString()
            {
                return Name;
            }
        }

        private string[] _strings;
        private int[] _buffer = new int[6000];

        public IEnumerable<string> CorpusList()
        {
            yield return "https://www.bartels.dev/high_freq.csv";
            yield return "https://www.bartels.dev/high_freq1.csv";
            yield return "https://www.nefsc.noaa.gov/drifter/drift_180351431.csv";
            yield return "http://www.transparency.ri.gov/awards/awardsummary.csv";
            yield return "https://www.census.gov/econ/bfs/csv/date_table.csv";
            yield return "https://www.sba.gov/sites/default/files/aboutsbaarticle/FY16_SBA_RAW_DATA.csv";
            yield return "https://wfmi.nifc.gov/fire_reporting/annual_dataset_archive/1972-2010/_WFMI_Big_Files/BOR_1972-2010_Gis.csv";
        }

        [ParamsSource("CorpusList")]
        public string CorpusUri { get; set; }

        [ParamsSource("GetImpls")]
        public Impl Impls { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _strings = GetStringsFromCorpus().GetAwaiter().GetResult();
        }

        public IEnumerable<Impl> GetImpls()
        {
            yield return new Impl(SplitEnumerator, "span_split");
            yield return new Impl(Buffered2, "buffered");
            yield return new Impl(SplitDefault, "string_split");
            yield return new Impl(FastBuffSplitEnum, "chunked");
            //yield return new Impl(BuffSplitEnum, "buff_old");
        }

        private async Task<string[]> GetStringsFromCorpus()
        {
            var response = await new HttpClient().GetAsync(CorpusUri);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();

            List<string> lines = new List<string>();

            StringReader reader = new StringReader(body);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }

            return lines.ToArray();
        }

        [Benchmark]
        public void SplitCsv()
        {
            var impl = Impls.Value;
            string[] lines = _strings;
            for (int i = 0; i < lines.Length; i++)
            {
                impl(lines[i]);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int calc(ReadOnlySpan<char> t)
        {
            int sum = 0;

            for (int i = 0; i < t.Length; i++) {
                sum += 1;
            }
            return sum;
        }

        public int BuffSplitEnum(string str)
        {
            int sum = 0;
            foreach (var t in SpanSplit.MemoryExtensions.SplitBuff(str, ',', _buffer))
            { 
                sum += calc(str.AsSpan()[t]);
            }
            return sum;
        }


        [SkipLocalsInit]
        public int Buffered2(string str)
        {
            int sum = 0;
            foreach (var t in new BufferedSplitEnumerator2(str, ',', _buffer))
            { 
                sum += calc(str.AsSpan()[t]);
            }
            return sum;
        }

        private int SplitEnumerator(string str)
        {
            int sum = 0;
            foreach (var t in SpanSplit.MemoryExtensions.Split(str, ','))
            { 
                sum += calc(str.AsSpan()[t]);
            }
            return sum;
        }


        [SkipLocalsInit]
        public int FastBuffSplitEnum(string str)
        {
            ReadOnlySpan<char> stri = str;
            Span<int> t = stackalloc int[1000];
            var enumer = new ChunkSplitEnumerator(str, ',', t);
            int previous = 0;
            int current = 0;
            int sum = 0;
            foreach(var chunk in enumer)
            {
                for (int i = 0; i < chunk.Length; i++) {
                    current = chunk[i];
                    sum += calc(stri.Slice(previous, current - previous));
                    previous = current + 1;
                }
            }

            return sum;
        }

        public int SplitDefault(string str)
        {
            int sum = 0;
            foreach (var t in str.Split(','))
            { 
                sum += calc(t.AsSpan()); 
            }
            return sum;
        }
    }
}