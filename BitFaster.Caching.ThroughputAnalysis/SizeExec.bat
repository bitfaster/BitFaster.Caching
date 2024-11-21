cls

@echo off
set DOTNET_Thread_UseAllCpuGroups=1

call BitFaster.Caching.ThroughputAnalysis.exe 1 100
call BitFaster.Caching.ThroughputAnalysis.exe 1 10000
call BitFaster.Caching.ThroughputAnalysis.exe 1 1000000
call BitFaster.Caching.ThroughputAnalysis.exe 1 10000000

call BitFaster.Caching.ThroughputAnalysis.exe 2 1000
call BitFaster.Caching.ThroughputAnalysis.exe 2 100000
call BitFaster.Caching.ThroughputAnalysis.exe 2 10000000
call BitFaster.Caching.ThroughputAnalysis.exe 2 100000000

call BitFaster.Caching.ThroughputAnalysis.exe 4 100
call BitFaster.Caching.ThroughputAnalysis.exe 4 10000
call BitFaster.Caching.ThroughputAnalysis.exe 4 1000000
call BitFaster.Caching.ThroughputAnalysis.exe 4 10000000

call BitFaster.Caching.ThroughputAnalysis.exe 8 100
call BitFaster.Caching.ThroughputAnalysis.exe 8 10000
call BitFaster.Caching.ThroughputAnalysis.exe 8 1000000
call BitFaster.Caching.ThroughputAnalysis.exe 8 10000000