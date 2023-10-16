using System;
using System.Collections.Generic;

namespace BitFaster.Caching
{
    internal class ConcurrentDictionarySize
    {
        internal static int NextPrimeGreaterThan(int min)
        {
            foreach (int prime in Primes)
            {
                if (prime > min)
                {
                    return prime;
                }
            }

            return 197;
        }

        /// <summary>
        /// Estimate the size of the ConcurrentDictionary constructor capacity arg to use for the given desired size.
        /// </summary>
        /// <remarks>
        /// To minimize collisions, ideal case is is for ConcurrentDictionary to have a prime number of buckets, and 
        /// for the bucket count to be about 30% than the cache capacity.
        /// </remarks>
        /// <param name="desiredSize">The desired cache size</param>
        /// <returns>The estimated optimal ConcurrentDictionary capacity</returns>
        internal static int Estimate(int desiredSize)
        {
            // When small, exact size hashtable to nearest larger prime number
            if (desiredSize <= 197)
            {
                return NextPrimeGreaterThan(desiredSize);
            }

            // When large, size to approx 10% of desired size to save memory. Initial value is chosen such
            // that 3x ConcurrentDictionary resize operations will select a prime number slightly larger
            // than desired size.
            foreach (var pair in SizeMap)
            {
                if (pair.Key > desiredSize)
                {
                    return pair.Value;
                }
            }

            // TODO: is this reasonable for large hashtables? Check if it will resize to max array size.
            return 250478587;
        }

#if NETSTANDARD2_0
        internal static int[] Primes = new int[] {
#else
        internal static ReadOnlySpan<int> Primes => new int[] {
#endif
            7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197
        };

#if NETSTANDARD2_0
        internal static KeyValuePair<int, int>[] SizeMap =
#else
        internal static ReadOnlySpan<KeyValuePair<int, int>> SizeMap =>
#endif
            new KeyValuePair<int, int>[129]
            {
                new KeyValuePair<int, int>(197, 197),
                new KeyValuePair<int, int>(277, 137),
                new KeyValuePair<int, int>(331, 163),
                new KeyValuePair<int, int>(359, 179),
                new KeyValuePair<int, int>(397, 197),
                new KeyValuePair<int, int>(443, 221),
                new KeyValuePair<int, int>(499, 247),
                new KeyValuePair<int, int>(557, 137),
                new KeyValuePair<int, int>(599, 149),
                new KeyValuePair<int, int>(677, 167),
                new KeyValuePair<int, int>(719, 179),
                new KeyValuePair<int, int>(797, 197),
                new KeyValuePair<int, int>(839, 209),
                new KeyValuePair<int, int>(887, 221),
                new KeyValuePair<int, int>(1061, 131),
                new KeyValuePair<int, int>(1117, 137),
                new KeyValuePair<int, int>(1237, 151),
                new KeyValuePair<int, int>(1439, 179),
                new KeyValuePair<int, int>(1559, 193),
                new KeyValuePair<int, int>(1777, 221),
                new KeyValuePair<int, int>(2011, 247),
                new KeyValuePair<int, int>(2179, 269),
                new KeyValuePair<int, int>(2347, 289),
                new KeyValuePair<int, int>(2683, 331),
                new KeyValuePair<int, int>(2797, 347),
                new KeyValuePair<int, int>(3359, 419),
                new KeyValuePair<int, int>(3917, 487),
                new KeyValuePair<int, int>(4363, 541),
                new KeyValuePair<int, int>(4597, 571),
                new KeyValuePair<int, int>(5879, 733),
                new KeyValuePair<int, int>(7517, 937),
                new KeyValuePair<int, int>(8731, 1087),
                new KeyValuePair<int, int>(9839, 1229),
                new KeyValuePair<int, int>(17467, 2179),
                new KeyValuePair<int, int>(18397, 2297),
                new KeyValuePair<int, int>(20357, 2543),
                new KeyValuePair<int, int>(24317, 3037),
                new KeyValuePair<int, int>(25919, 3239),
                new KeyValuePair<int, int>(29759, 3719),
                new KeyValuePair<int, int>(31357, 3917),
                new KeyValuePair<int, int>(33599, 4199),
                new KeyValuePair<int, int>(38737, 4841),
                new KeyValuePair<int, int>(41117, 5137),
                new KeyValuePair<int, int>(48817, 6101),
                new KeyValuePair<int, int>(61819, 7723),
                new KeyValuePair<int, int>(72959, 9119),
                new KeyValuePair<int, int>(86011, 10747),
                new KeyValuePair<int, int>(129277, 16157),
                new KeyValuePair<int, int>(140797, 17597),
                new KeyValuePair<int, int>(164477, 20557),
                new KeyValuePair<int, int>(220411, 27547),
                new KeyValuePair<int, int>(233851, 29227),
                new KeyValuePair<int, int>(294397, 36797),
                new KeyValuePair<int, int>(314879, 39359),
                new KeyValuePair<int, int>(338683, 42331),
                new KeyValuePair<int, int>(389117, 48637),
                new KeyValuePair<int, int>(409597, 51197),
                new KeyValuePair<int, int>(436477, 54557),
                new KeyValuePair<int, int>(609277, 76157),
                new KeyValuePair<int, int>(651517, 81437),
                new KeyValuePair<int, int>(737279, 92159),
                new KeyValuePair<int, int>(849917, 106237),
                new KeyValuePair<int, int>(1118203, 139771),
                new KeyValuePair<int, int>(1269757, 158717),
                new KeyValuePair<int, int>(1440763, 180091),
                new KeyValuePair<int, int>(1576957, 197117),
                new KeyValuePair<int, int>(1684477, 210557),
                new KeyValuePair<int, int>(2293757, 286717),
                new KeyValuePair<int, int>(2544637, 318077),
                new KeyValuePair<int, int>(2666491, 333307),
                new KeyValuePair<int, int>(2846717, 355837),
                new KeyValuePair<int, int>(3368957, 421117),
                new KeyValuePair<int, int>(3543037, 442877),
                new KeyValuePair<int, int>(4472827, 559099),
                new KeyValuePair<int, int>(4710397, 588797),
                new KeyValuePair<int, int>(5038079, 629759),
                new KeyValuePair<int, int>(5763067, 720379),
                new KeyValuePair<int, int>(6072317, 759037),
                new KeyValuePair<int, int>(6594557, 824317),
                new KeyValuePair<int, int>(7913467, 989179),
                new KeyValuePair<int, int>(8257531, 1032187),
                new KeyValuePair<int, int>(9175037, 1146877),
                new KeyValuePair<int, int>(9633787, 1204219),
                new KeyValuePair<int, int>(10076159, 1259519),
                new KeyValuePair<int, int>(11386877, 1423357),
                new KeyValuePair<int, int>(14020603, 1752571),
                new KeyValuePair<int, int>(16056317, 2007037),
                new KeyValuePair<int, int>(19496957, 2437117),
                new KeyValuePair<int, int>(20848637, 2606077),
                new KeyValuePair<int, int>(24084479, 3010559),
                new KeyValuePair<int, int>(27934717, 3491837),
                new KeyValuePair<int, int>(29589499, 3698683),
                new KeyValuePair<int, int>(32788477, 4098557),
                new KeyValuePair<int, int>(36044797, 4505597),
                new KeyValuePair<int, int>(38051837, 4756477),
                new KeyValuePair<int, int>(43581437, 5447677),
                new KeyValuePair<int, int>(51814397, 6476797),
                new KeyValuePair<int, int>(56688637, 7086077),
                new KeyValuePair<int, int>(60948479, 7618559),
                new KeyValuePair<int, int>(69631997, 8703997),
                new KeyValuePair<int, int>(75366397, 9420797),
                new KeyValuePair<int, int>(78643199, 9830399),
                new KeyValuePair<int, int>(96337919, 12042239),
                new KeyValuePair<int, int>(106168319, 13271039),
                new KeyValuePair<int, int>(115671037, 14458877),
                new KeyValuePair<int, int>(132382717, 16547837),
                new KeyValuePair<int, int>(144179197, 18022397),
                new KeyValuePair<int, int>(165150719, 20643839),
                new KeyValuePair<int, int>(178257917, 22282237),
                new KeyValuePair<int, int>(188743679, 23592959),
                new KeyValuePair<int, int>(209715197, 26214397),
                new KeyValuePair<int, int>(254279677, 31784957),
                new KeyValuePair<int, int>(297271291, 37158907),
                new KeyValuePair<int, int>(314572799, 39321599),
                new KeyValuePair<int, int>(385351679, 48168959),
                new KeyValuePair<int, int>(453509117, 56688637),
                new KeyValuePair<int, int>(517472251, 64684027),
                new KeyValuePair<int, int>(644874239, 80609279),
                new KeyValuePair<int, int>(673710077, 84213757),
                new KeyValuePair<int, int>(770703359, 96337919),
                new KeyValuePair<int, int>(849346559, 106168319),
                new KeyValuePair<int, int>(903086077, 112885757),
                new KeyValuePair<int, int>(1145044987, 143130619),
                new KeyValuePair<int, int>(1233125371, 154140667),
                new KeyValuePair<int, int>(1321205759, 165150719),
                new KeyValuePair<int, int>(1394606077, 174325757),
                new KeyValuePair<int, int>(1635778559, 204472319),
                new KeyValuePair<int, int>(1855979519, 231997439),
                new KeyValuePair<int, int>(2003828731, 250478587),
            };
    }
}
