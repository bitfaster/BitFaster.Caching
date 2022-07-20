using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.HitRateAnalysis.Wikibench
{
    public class WikiBenchFile
    {
        public WikiBenchFile(int id, Uri uri)
        {
            this.Id = id;
            this.Uri = uri;
            this.FilePath = $"currenttmp{id}";
        }

        public int Id { get; }

        public Uri Uri { get; }

        public string FilePath { get; }

        public async Task DownloadIfNotExistsAsync()
        {
            var zipped = this.FilePath + ".gz";

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

            if (!File.Exists(this.FilePath))
            {
                Console.WriteLine($"Decompressing {Uri}...");

                using FileStream originalFileStream = new FileInfo(zipped).OpenRead();
                using var decompressedFileStream = File.Create(this.FilePath);
                using var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);

                decompressionStream.CopyTo(decompressedFileStream);
            }
        }

        public IEnumerable<Uri> EnumerateUris()
        {
            using StreamReader sr = new StreamReader(this.FilePath);

            while (sr.Peek() >= 0)
            {
                var line = sr.ReadLine();

                // reads end with -
                if (line?.EndsWith('-') ?? false)
                {
                    var parsed = ParseLine(line);
                    if (parsed != null)
                    {
                        if (Uri.TryCreate(parsed, UriKind.Relative, out var result))
                        { 
                            yield return result; 
                        }
                    }
                }
            }
        }

        private static readonly string[] containsFilters = 
            { 
                "?search = ",
                "User+talk",
                "User_talk",
                "User_talk",
                "&search=",
                "User+talk",
                "User_talk",
                "User:",
                "Talk:",
                "&diff=",
                "&action=rollback",
                "Special:Watchlist",
            };

        private static readonly string[] startswithFilters =
            {
                "/wiki/Special:Search", 
                "/w/query.php", 
                "/wiki/Talk:", 
                "/wiki/Special:AutoLogin",
                "/Special:UserLogin", 
                "/w/api.php", 
                "/error:"
            };

        private static string? ParseLine(string line)
        {
            // Example lines
            // 929840853 1190146243.326 http://upload.wikimedia.org/wikipedia/en/thumb/e/e4/James_Johnson.jpg/200px-James_Johnson.jpg -
            // 929840930 1190146243.320 http://meta.wikimedia.org/w/index.php?title=MediaWiki:Wikiminiatlas.js&action=raw&ctype=text/javascript&smaxage=21600&maxage=86400 -

            if (line.Length < 25 + 7)
            {
                return null;
            }

            int start = line.IndexOf('/', 25+7);

            if (start == -1)
            {
                return null;
            }

            var url = line.Substring(start, line.Length - start - 2);

            url = url.Replace("%2F", "/", StringComparison.Ordinal);
            url = url.Replace("%20", " ", StringComparison.Ordinal);
            url = url.Replace("&amp;", "&", StringComparison.Ordinal);
            url = url.Replace("%3A", ":", StringComparison.Ordinal);

            foreach (var filter in startswithFilters)
            {
                if (url.StartsWith(filter, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            foreach (var filter in containsFilters)
            {
                if (url.Contains(filter, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return url;
        }
    }
}
