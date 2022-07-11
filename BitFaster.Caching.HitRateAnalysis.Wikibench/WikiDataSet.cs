using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.HitRateAnalysis.Wikibench
{
    public class WikiDataSet
    {
        List<WikiBenchFile> files = new List<WikiBenchFile>();

        public WikiDataSet(IEnumerable<string> urls)
        {
            int n = 1;
            foreach (string url in urls)
            {
                files.Add(new WikiBenchFile(n++, new Uri(url)));
            }
        }

        public async Task DownloadIfNotExistsAsync()
        {
            var tasks = new List<Task>();

            foreach (var f in files)
            {
                tasks.Add(Task.Run(async () => await f.DownloadIfNotExistsAsync()));
            }

            await Task.WhenAll(tasks);
        }

        public IEnumerable<Uri> EnumerateUris()
        {
            foreach (var f in files)
            {
                foreach (var uri in f.EnumerateUris())
                {
                    yield return uri;
                }
            }
        }
    }
}
