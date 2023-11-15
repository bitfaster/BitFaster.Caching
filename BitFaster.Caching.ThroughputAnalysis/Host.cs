using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Environments;

namespace BitFaster.Caching.ThroughputAnalysis
{
    public class Host
    {
        public static void PrintInfo()
        {
            var hostinfo = HostEnvironmentInfo.GetCurrent();

            foreach (var segment in hostinfo.ToFormattedString())
            {
                string toPrint = segment;

                // remove benchmark dot net
                if (toPrint.StartsWith("Ben"))
                {
                    toPrint = segment.Substring(segment.IndexOf(',') + 2, segment.Length - segment.IndexOf(',') - 2);
                }

                Console.WriteLine(toPrint);
            }

            Console.WriteLine();
            Console.WriteLine($"Available CPU Count: {Host.GetAvailableCoreCount()}");

            if (Host.GetLogicalCoreCount() > Host.GetAvailableCoreCount())
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;

                Console.WriteLine("WARNING: not all cores available.");
                Console.WriteLine($"DOTNET_Thread_UseAllCpuGroups: {Environment.GetEnvironmentVariable("DOTNET_Thread_UseAllCpuGroups") ?? "Not Set (disabled)"}");

                Console.ResetColor();
            }

            Console.WriteLine();
        }

        public static int GetAvailableCoreCount()
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("DOTNET_Thread_UseAllCpuGroups") ?? "0", out int useAllGroups))
            {
                if (useAllGroups == 1)
                {
                    return GetLogicalCoreCount();
                }
            }

            return Environment.ProcessorCount;
        }

        /// <summary>
        /// Get the exact physical core count on Windows
        /// </summary>
        public unsafe static int GetLogicalCoreCount()
        {
            // don't crash
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.ProcessorCount;
            }

            uint len = 0;
            const int ERROR_INSUFFICIENT_BUFFER = 122;

            if (!GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, IntPtr.Zero, ref len) &&
                Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER)
            {
                // Allocate that much space
                var buffer = new byte[len];
                fixed (byte* bufferPtr = buffer)
                {
                    // Call GetLogicalProcessorInformationEx with the allocated buffer
                    if (GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, (IntPtr)bufferPtr, ref len))
                    {
                        // Walk each SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX in the buffer, where the Size of each dictates how
                        // much space it's consuming.  For each group relation, count the number of active processors in each of its group infos.
                        int processorCount = 0;
                        byte* ptr = bufferPtr;
                        byte* endPtr = bufferPtr + len;
                        while (ptr < endPtr)
                        {
                            var current = (SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX*)ptr;
                            if (current->Relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore)
                            {
                                // Flags is 0 if the core has a single logical proc, LTP_PC_SMT if more than one
                                // for now, assume "more than 1" == 2, as it has historically been for hyperthreading
                                processorCount += (current->Processor.Flags == 0) ? 1 : 2;
                            }
                            ptr += current->Size;
                        }
                        return processorCount;
                    }
                }
            }

            return -1;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP RelationshipType, IntPtr Buffer, ref uint ReturnedLength);
    }

#pragma warning disable 0649, 0169
    internal enum LOGICAL_PROCESSOR_RELATIONSHIP
    {
        RelationProcessorCore,
        RelationNumaNode,
        RelationCache,
        RelationProcessorPackage,
        RelationGroup,
        RelationAll = 0xffff
    }

    internal struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
    {
        public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
        public uint Size;
        public PROCESSOR_RELATIONSHIP Processor;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PROCESSOR_RELATIONSHIP
    {
        public byte Flags;
        private byte EfficiencyClass;
        private fixed byte Reserved[20];
        public ushort GroupCount;
        public IntPtr GroupInfo;
    }
#pragma warning restore 0169, 0149
}
