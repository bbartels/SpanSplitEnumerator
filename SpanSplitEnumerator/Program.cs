using System.Threading.Tasks;
using BenchmarkDotNet.Running;

namespace SpanSplit
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<StringSplitRunner>();
        }
    }
}
