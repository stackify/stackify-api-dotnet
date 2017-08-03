dotnet pack StackifyLib\project.json  -c Release -o ..\BuildOutput
dotnet pack StackifyLib.AspNetCore\project.json  -c Release -o ..\BuildOutput
dotnet pack NLog.Targets.Stackify\project.json  -c Release -o ..\BuildOutput
dotnet pack StackifyLib.CoreLogger\project.json  -c Release -o ..\BuildOutput
dotnet pack StackifyLib.log4net\project.json  -c Release -o ..\BuildOutput
dotnet pack StackifyLib.StackifyTraceListener\project.json  -c Release -o ..\BuildOutput

dotnet pack C:\BitBucket\Serilog.Sinks.Stackify\Serilog.Sinks.Stackify\project.json  -c Release -o C:\BitBucket\stackify-api-dotnet\BuildOutput