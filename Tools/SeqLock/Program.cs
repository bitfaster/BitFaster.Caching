using SeqLock;

Console.WriteLine("Torn write test");

while (true)
{ 
    Console.WriteLine("Enter");
    Console.WriteLine("1 for repro");
    Console.WriteLine("2 for SeqLock (no repro)");
    string? s = Console.ReadLine();

    if (int.TryParse(s, out var i))
    { 
        switch (i)
        {
            case 1:
                // repros
                new TornProgram().run();
                break;
            case 2:
                // does not repro
                new TornProgramSeqLock().run();
                break;
            default:
                Console.WriteLine("Input must be either 1 or 2");
                break;
        }
    }
}
