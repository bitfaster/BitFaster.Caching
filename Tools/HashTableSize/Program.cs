using HashTableSize;

Console.WriteLine("Starting...");

var map = new List<KeyValuePair<int, int>>();

foreach (int prime in HashTools.Primes)
{
    Console.WriteLine($"Evaluating {prime}...");

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

var q = from s in map
        where s.Value < s.Key * 0.2
        where s.Key > 167
        orderby s.Key
        group s by s.Key into g
        select new { Size = g.Key, Start = g.Max(s => HashTools.ChooseInitialSize(s.Key, s.Value)) };


Console.WriteLine();
Console.WriteLine($"internal static KeyValuePair<int, int>[] Map = new KeyValuePair<int, int>[{q.Count()}]");
Console.WriteLine("{");

int lastSize = 1;
int count = 0;

foreach (var pair in q)
{
    var diff = (float)pair.Size / lastSize;
    if (diff > 1.04)
    { 
        Console.WriteLine($"    new KeyValuePair<int, int>({pair.Size}, {pair.Start}),");
        count++;
    }

    lastSize = pair.Size;
}

Console.WriteLine("};");
Console.WriteLine(count);