using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public class Exporter
    {
        DataTable resultTable = new DataTable();

        public Exporter(int maxThreads)
        {
            // output:
            // ThreadCount   1  2  3  4  5
            // Classic       5  6  7  7  8
            // Concurrent    5  6  7  7  8

            resultTable.Clear();
            resultTable.Columns.Add("ThreadCount");
            foreach (var tc in Enumerable.Range(1, maxThreads).ToArray())
            {
                resultTable.Columns.Add(tc.ToString());
            }
        }

        public void Initialize(IEnumerable<ICacheFactory> caches)
        {
            foreach (var c in caches)
            {
                c.DataRow = resultTable.NewRow();
                c.DataRow["Class"] = c.Name;
            }
        }

        public void CaptureRows(IEnumerable<ICacheFactory> caches)
        {
            foreach (var c in caches)
            {
                resultTable.Rows.Add(c.DataRow);
            }
        }

        public void ExportCsv(Mode mode)
        {
            using (var textWriter = File.CreateText($"Results{mode}.csv"))
            using (var csv = new CsvWriter(textWriter, CultureInfo.InvariantCulture))
            {
                foreach (DataColumn column in resultTable.Columns)
                {
                    csv.WriteField(column.ColumnName);
                }
                csv.NextRecord();

                foreach (DataRow row in resultTable.Rows)
                {
                    for (var i = 0; i < resultTable.Columns.Count; i++)
                    {
                        csv.WriteField(row[i]);
                    }
                    csv.NextRecord();
                }
            }
        }
    }
}
