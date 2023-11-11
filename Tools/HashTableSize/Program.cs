using HashTableSize;

Console.WriteLine("Starting...");

var map = new List<KeyValuePair<int, int>>();

foreach (int prime in HashTools.Primes)
{
    int p = prime;
    while (p < int.MaxValue) 
    {
        if (!HashTools.TryNextTableSize(p, out int t))
        {
            break;
        }

        var f = HashTools.Factor(t);

        // 0 factors = prime
        if (f.Count == 0)
        {
            map.Add(new KeyValuePair<int, int>(t, prime));
        }

        p = t;
    }
}

map = map
    .Where(m => m.Value < m.Key * 0.2)  // initial values less than 20%
    .Where(m => m.Key > 167)            // start above 167
    .OrderBy(m => m.Key)
    .GroupBy(                           // take the best initial size for each prime
        m => m.Key,
        m => m.Value,
        (size, startSizes) => new KeyValuePair<int, int>
        ( 
            size,
            startSizes.Select(i => HashTools.ChooseInitialSize(size, i)).Max())
        )
    .ToList();

// filter the map to successive values with more than a 4% difference (to reduce lookup table size)
map = map.Take(1).Concat(
    map
    .Skip(1)
    .Select((x, i) => new { x.Key, x.Value, diff = (float)x.Key / map[i].Key })
    .Where(x => x.diff > 1.04)
    .Select(x => new KeyValuePair<int, int>(x.Key, x.Value)))
    .ToList();

// print the declaration of the lookup table to the console
Console.WriteLine();
Console.WriteLine($"internal static KeyValuePair<int, int>[] Map = new KeyValuePair<int, int>[{map.Count}]");
Console.WriteLine("{");

foreach (var pair in map)
{
    Console.WriteLine($"    new KeyValuePair<int, int>({pair.Key}, {pair.Value}),");
}

Console.WriteLine("};");

