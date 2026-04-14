using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.HitRateAnalysis.Glimpse
{
    // TODO: dedupe
    public class DataFile
    {
        private static readonly Uri Uri = new Uri("https://github.com/bitfaster/cache-datasets/releases/download/v1.0/gli.trace.gz");
        private static readonly string FilePath = "gli.trace";

        public static async Task DownloadIfNotExistsAsync()
        {
            var zipped = FilePath + ".gz";

            if (!File.Exists(zipped))
            {
                Console.WriteLine($"Downloading {Uri}...");
                HttpClient client = new HttpClient();
                var response = await client.GetAsync(Uri);
                using (var fs = new FileStream(zipped, FileMode.CreateNew))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            if (!File.Exists(FilePath))
            {
                Console.WriteLine($"Decompressing {Uri}...");

                using FileStream originalFileStream = new FileInfo(zipped).OpenRead();
                using var decompressedFileStream = File.Create(FilePath);
                using var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);

                decompressionStream.CopyTo(decompressedFileStream);
            }
        }

        public static IEnumerable<long> EnumerateFileData()
        {
            // File data is like this:
            //0
            //1
            //2
            //3
            //4
            //5
            //6

            using StreamReader sr = new StreamReader(FilePath);

            while (sr.Peek() >= 0)
            {
                var line = sr.ReadLine();

                if (long.TryParse(line, out var value))
                {
                    yield return value;
                }
            }
        }
    }
}
