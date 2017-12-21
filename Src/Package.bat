dotnet pack StackifyLib\StackifyLib.csproj  -c Release -o ..\BuildOutput
dotnet pack StackifyLib.AspNetCore\StackifyLib.AspNetCore.csproj  -c Release -o ..\BuildOutput
dotnet pack NLog.Targets.Stackify\NLog.Targets.Stackify.csproj  -c Release -o ..\BuildOutput
dotnet pack StackifyLib.CoreLogger\StackifyLib.CoreLogger.csproj  -c Release -o ..\BuildOutput
dotnet pack StackifyLib.log4net\StackifyLib.log4net.csproj  -c Release -o ..\BuildOutput
dotnet pack StackifyLib.StackifyTraceListener\StackifyLib.StackifyTraceListener.csproj  -c Release -o ..\BuildOutput

REM dotnet pack C:\BitBucket\Serilog.Sinks.Stackify\Serilog.Sinks.Stackify\project.json  -c Release -o C:\BitBucket\stackify-api-dotnet\BuildOutput