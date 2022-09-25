
namespace BitFaster.Caching.ThroughputAnalysis
{
    public class CommandParser
    {
        public static (Mode, int) Parse(string[] args)
        {
            // arg[0] == mode, arg[1] == size
            if (args.Length == 2)
            {
                if (int.TryParse(args[0], out int modeArg))
                {
                    if (int.TryParse(args[1], out int size))
                    {
                        return ((Mode)modeArg, size);
                    }
                }
            }

            Mode mode = Mode.Read;
            var menu = new EasyConsole.Menu()
                .Add("Read", () => mode = Mode.Read)
                .Add("Read + Write", () => mode = Mode.ReadWrite)
                .Add("Update", () => mode = Mode.Update)
                .Add("Evict", () => mode = Mode.Evict)
                .Add("All", () => mode = Mode.All);

            menu.Display();

            return (mode, 500);
        }
    }
}
