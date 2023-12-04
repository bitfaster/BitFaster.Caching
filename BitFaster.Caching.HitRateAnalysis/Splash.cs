using System;

namespace BitFaster.Caching.HitRateAnalysis
{
    internal static class Splash
    {
        public static void Display()
        {
            string branch = $"({ThisAssembly.Git.Branch}" + (ThisAssembly.Git.IsDirty ? " dirty)" : ")");

            Console.WriteLine($"Hit Rate Analysis {ThisAssembly.Git.BaseTag} {ThisAssembly.Git.Commit} {branch}");
            Console.WriteLine();
        }
    }
}
