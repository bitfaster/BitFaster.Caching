cls

@echo off
set DOTNET_Thread_UseAllCpuGroups=1

call BitFaster.Caching.ThroughputAnalysis.exe 2 1000

call BitFaster.Caching.ThroughputAnalysis.exe 4 100
call BitFaster.Caching.ThroughputAnalysis.exe 4 10000
call BitFaster.Caching.ThroughputAnalysis.exe 4 1000000
call BitFaster.Caching.ThroughputAnalysis.exe 4 10000000