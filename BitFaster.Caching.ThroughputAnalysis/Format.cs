namespace BitFaster.Caching.ThroughputAnalysis
{
    internal class Format
    {
        public static string Throughput(double thru)
        {
            string dformat = "0.00;-0.00";
            string raw = thru.ToString(dformat);
            return raw.PadLeft(7, ' ');
        }
    }
}
