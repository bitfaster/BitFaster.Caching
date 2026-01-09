using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.HitRateAnalysis.Arc
{
    public class ArcDataFile
    {
        // See https://researcher.watson.ibm.com/researcher/view_person_subpage.php?id=4700
        private readonly Uri Uri;
        private readonly string FilePath = "DS1.lis";

        // Trace file taken from:
        // Nimrod Megiddo and Dharmendra S.Modha, "ARC: A Self-Tuning, Low Overhead Replacement Cache," USENIX Conference on File and Storage Technologies(FAST 03), San Francisco, CA, pp. 115-130, March 31-April 2, 2003. 

        public ArcDataFile(Uri uri)
        {
            this.Uri = uri;
            this.FilePath = ComputePath(uri);
        }

        private static string ComputePath(Uri uri)
        {
            string seg = uri.Segments.LastOrDefault();

            if (seg == null)
            {
                throw new InvalidOperationException();
            }

            if (seg.EndsWith(".gz"))
            {
                seg = seg.Substring(0, seg.LastIndexOf(".gz"));
            }

            return seg;
        }

        public async Task DownloadIfNotExistsAsync()
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

        public IEnumerable<long> EnumerateFileData()
        {
            //   File Format: 
            //   Every line in every file has four fields.
            //
            //   First field: starting_block
            //   Second field: number_of_blocks(each block is 512 bytes)
            //
            //   Third field: 	ignore
            //   Fourth field: request_number(starts at 0)
            //
            //
            //   Example: first line in P6.lis is
            //   110765 64 0 0
            //
            //
            //   110765  starting block
            //
            //   64      64 blocks each of 512 bytes
            //           so this represents 64 requests(each of a 512 byte page) from 110765 to 110828
            //
            //   0       ignore
            //
            //   0       request number(goes from 0 to n-1)

            using StreamReader sr = new StreamReader(FilePath);

            while (sr.Peek() >= 0)
            {
                var line = sr.ReadLine();
                var chunks = line.Split(' ');

                if (long.TryParse(chunks[0], out var startBlock))
                {
                    if (int.TryParse(chunks[1], out var sequence))
                    {
                        for (long i = startBlock; i < startBlock + sequence; i++)
                        {
                            yield return i;
                        }
                    }
                }
            }
        }
    }
}
