using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BitFaster.Caching.ThroughputAnalysis
{
    // Taken from BenchmarkDotNet here:
    // https://github.com/dotnet/BenchmarkDotNet/blob/5557aee0638bda38001bd6c2000164d9b96d315a/src/BenchmarkDotNet/Running/PowerManagementApplier.cs#L45
    // https://github.com/dotnet/BenchmarkDotNet/blob/5557aee0638bda38001bd6c2000164d9b96d315a/src/BenchmarkDotNet/Helpers/PowerManagementHelper.cs#L9
    internal class PowerPlan
    {
        public static void EnableHighPerformance()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var highPerformancePlanId = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

            if (CurrentPlan != highPerformancePlanId)
            {
                Console.WriteLine($"Current power plan is {CurrentPlanFriendlyName}");

                if (PowerSetActiveScheme(IntPtr.Zero, ref highPerformancePlanId) == 0)
                {
                    Console.WriteLine($"Switched to High Performance power plan.");
                }
            }
            else
            {
                Console.WriteLine($"Current Windows power plan = {CurrentPlanFriendlyName}");
            }
        }

        internal static Guid? CurrentPlan
        {
            get
            {
                IntPtr activeGuidPtr = IntPtr.Zero;
                uint res = PowerGetActiveScheme(IntPtr.Zero, ref activeGuidPtr);
                if (res != SuccessCode)
                    return null;

                return (Guid)Marshal.PtrToStructure(activeGuidPtr, typeof(Guid));
            }
        }

        internal static string CurrentPlanFriendlyName
        {
            get
            {
                uint buffSize = 0;
                StringBuilder buffer = new StringBuilder();
                IntPtr activeGuidPtr = IntPtr.Zero;
                uint res = PowerGetActiveScheme(IntPtr.Zero, ref activeGuidPtr);
                if (res != SuccessCode)
                    return null;
                res = PowerReadFriendlyName(IntPtr.Zero, activeGuidPtr, IntPtr.Zero, IntPtr.Zero, buffer, ref buffSize);
                if (res == ErrorMoreData)
                {
                    buffer.Capacity = (int)buffSize;
                    res = PowerReadFriendlyName(IntPtr.Zero, activeGuidPtr,
                        IntPtr.Zero, IntPtr.Zero, buffer, ref buffSize);
                }
                if (res != SuccessCode)
                    return null;

                return buffer.ToString();
            }
        }

        private const uint ErrorMoreData = 234;
        private const uint SuccessCode = 0;

        [DllImport("powrprof.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint PowerReadFriendlyName(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, IntPtr PowerSettingGuid, StringBuilder Buffer, ref uint BufferSize);

        [DllImport("powrprof.dll", ExactSpelling = true)]
        private static extern int PowerSetActiveScheme(IntPtr ReservedZero, ref Guid policyGuid);

        [DllImport("powrprof.dll", ExactSpelling = true)]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, ref IntPtr ActivePolicyGuid);
    }
}
