# Stackify API for .NET

[![Build status](https://ci.appveyor.com/api/projects/status/6suxse470ab4bdxl?svg=true)](https://ci.appveyor.com/project/jaredcnance/stackify-api-dotnet)
[![NuGet](https://img.shields.io/nuget/v/StackifyLib.svg)](https://www.nuget.org/packages/StackifyLib/)

Library for Stackify users to integrate Stackify in to their projects. Provides support for sending errors, logs, and custom metrics to Stackify. Also some support for querying metric data back out of Stackify for use in external projects.

**Important links:**
- [Stackify homepage](http://www.stackify.com)
- [Stackify documentation site](http://support.stackify.com/hc/en-us/categories/200398739-Errors-Logs)
- [NuGet packages](https://www.nuget.org/packages?q=Stackify)
- [Best practices for logging with C#](https://stackify.com/csharp-logging-best-practices/)
- [Why you should use tags in your logs](https://stackify.com/get-smarter-log-management-with-log-tags/)


**Read me sections:**
- [Basics](#basics)
- [Errors & Logs](#errors-and-logs)
- [StackifyLib NLog](#nlog-2012---v31)
- [StackifyLib log4net 2.0+](#log4net-v20-v1211)
- [StackifyLib log4net 1.2.10](#log4net-v1210)
- [Direct API](#direct-api)
- [Configuring with Azure service definitions](#configuring-with-azure-service-definitions)

##Basics

Several nuget packages are available for Stackify's core API as well as various logging frameworks. Please install the proper packages. All of the available packages for various logging frameworks are wrapper on top of StackifyLib which can also be used directly for logging. 

Features

 - Asynchronous logging framework for high performance
 - Automatic queueing and error fall back logic 
 - Support for NLog, log4net, ELMAH, and direct API usage
 - Can be plugged in to custom built or other logging frameworks
 - Does not depend on the Stackify monitoring agent. All data is sent directly to Stackify.

The packages for log4net, NLog and Elmah all depend on StackifyLib. StackifyLib also has support for custom metrics and accessing some of Stackify's API to pull data back out.

The following is required in your App.config or Web.config:

```xml
<appSettings>
        <add key="Stackify.ApiKey" value="YOUR API KEY HERE" />
        <add key="Stackify.AppName" value="YOUR APP NAME" />
        <add key="Stackify.Environment" value="OPTIONAL ENVIRONMENT NAME LIKE PROD, DEV" />
</appSettings>
```

Optionally, you can set the config settings on your machine's environment variables with the same configuration key and value.
	Example are executed in window's cmd as an admin:
```
setx Stackify.ApiKey "YOUR API KEY HERE" /m
setx Stackify.Environment "MY ENVIRONMENT HERE" /m
```

You can set the config settings in code like so which will override the appSettings configs as well.

```
StackifyLib.Logger.GlobalApiKey = "";
StackifyLib.Logger.GlobalAppName = "";
StackifyLib.Logger.GlobalEnvironment = "";
```

By default the library will use the WebRequest.DefaultWebProxy. If you want to set a specific proxy server only for StackifyLib, you can do so in code OR via config.

```xml
<appSettings>
        <add key="Stackify.ProxyServer" value="http://test:test@172.19.1.1:8888/" />
</appSettings>
```

```csharp
StackifyLib.Utils.HttpClient.CustomWebProxy = new WebProxy();
```

If you are having problems you can get logging out of the framework by hooking in to its custom logging.

```csharp
StackifyLib.Utils.StackifyAPILogger.LogEnabled = true;
StackifyLib.Utils.StackifyAPILogger.OnLogMessage += StackifyAPILogger_OnLogMessage;

static void StackifyAPILogger_OnLogMessage(string data)
{
        Debug.WriteLine(data);
}
```

Please note that Newtonsoft.Json is used by StackifyLib but is embedded as a resource to avoid version conflicts. Costura.Fody is being used to embed it. If you have any issues with Newtonsoft.Json as a result of using StackifyLib please contact Stackify support.

##Errors and Logs

If you log an object with the message, Stackify's log viewer makes it easy to search by these parameters. You can always search by the text in the log message itself, but searching by the logged properties provides a lot more power. If you always logged a "clientid" for example on every log message, you could search in Stackify for "json.clientid:1" and quickly see all logs and errors affecting that specific client. Another big difference and advantage to logging objects is you can do a range type search "json.clientid:[1 TO 10]" which would not be possible by a straight text search.


###NLog 2.0.1.2 - v3.1+

**Install via NuGet package**
```
PM> Install-Package NLog.Targets.Stackify
```

Sample config:
```xml
<nlog>
        <extensions>
                <add assembly="NLog.Targets.Stackify"/>
        </extensions>
        <targets>
                <target name="stackify" type="StackifyTarget" globalContextKeys="examplekey1,key2" 
                mappedContextKeys="" callContextKeys="" logMethodNames="true" />
        </targets>
        <rules>
                <logger name="*" writeTo="stackify" minlevel="Debug" />
        </rules>
</nlog>
```

Logging custom objects is supported and will be searchable in Stackify's log viewer

```csharp
static NLog.Logger nlog = NLog.LogManager.GetCurrentClassLogger();
Dictionary<string, object> dictionary = new Dictionary<string, object>();
dictionary["clientid"] = 1;
dictionary["color"] = "red";
nlog.Debug("Test message", dictionary);
```

Options

- GlobalContext and MappedContext keys are fully supported by setting the parameters in the config as a comma delimited list of keys. See sample config above.
- CallContextKeys is an additional feature unrelated to NLog that uses the local thread storage for more advanced tracking of context variables. It is used via CallContext.LogicalSetData(key, value). Research LogicalSetData online to learn more. It is supposed to work better across child Task objects and with async.
- logMethodNames - Method names will show up in the StackifyLog viewer most of the time as the class name that did the logging. For exceptions it will usually show the method name. To enable the exact method name for all logging, set this property to true. Note that it does cause a small performance hit due to walking the StackTrace.

###log4net v2.0+ (v1.2.11+)

**Install via NuGet package**
```
PM> Install-Package StackifyLib.log4net
```

Note: Nuget packages are compiled against 2.0.0 (1.2.11) but any newer version will work with a valid assembly binding redirect. log4net 2.0.3 is actually 1.2.13 which makes the binding redirect look strange.

```xml
<dependentAssembly>
        <assemblyIdentity name="log4net" publicKeyToken="669e0ddf0bb1aa2a" culture="neutral" />
        <bindingRedirect oldVersion="1.2.11.0-1.2.13.0" newVersion="1.2.13.0" />
</dependentAssembly>
```

Sample config:
```xml
<log4net>
        <root>
                <level value="DEBUG" />
                <appender-ref ref="StackifyAppender" />
        </root>
        <appender name="StackifyAppender" type="StackifyLib.log4net.StackifyAppender, StackifyLib.log4net">
                <globalContextKeys>examplekey1,key2</globalContextKeys>
                <threadContextKeys></threadContextKeys>
                <logicalThreadContextKeys></logicalThreadContextKeys>

                <callContextKeys></callContextKeys>

                <!-- If logging a very high rate of messages, disable logging method names for performance -->
                <logMethodNames>true</logMethodNames>

                <!-- Only log errors and fatals by using filters and setting levelMin and levelMax appropriately -->
                <!-- http://logging.apache.org/log4net/release/manual/configuration.html -->
                <filter type="log4net.Filter.LevelRangeFilter">
                        <levelMin value="DEBUG" />
                        <levelMax value="FATAL" />
                </filter>
        </appender>
</log4net>
```

Options

- GlobalContext, ThreadContext, and LogicalThreadContext keys are fully supported by setting the parameters in the config as a comma delimited list of keys. See sample config above.
- CallContextKeys is an additional feature unrelated to log4net that uses the local thread storage for more advanced tracking of context variables. LogicalThreadContext provides the same functionality but uses an internal property collection. We have seen instances where the serialization of that collection can cause exceptions. This was created as an alternative method to the built in function. It is used via CallContext.LogicalSetData(key, value). Research LogicalSetData online to learn more. It is supposed to work better across child Task objects and with async.
- logMethodNames - Method names will show up in the StackifyLog viewer most of the time as the class name that did the logging. For exceptions it will usually show the method name. To enable the exact method name for all logging, set this property to true. Note that it does cause a small performance hit due to walking the StackTrace.


####log4net Extension Methods

log4net does not internally have methods for logging a log message along with an object. Stackify's appenders work fine if you log an object directly or we have created some friendly extension methods to make it easy to log an object with your message at the same time.

```cshapr
using StackifyLib; //extension methods are here
static log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Program));
Dictionary<string, object> dictionary = new Dictionary<string, object>();
dictionary["clientid"] = 1;
dictionary["name"] = "process name";
logger.Debug("Starting some process for client 1"); //Normal basic log message works fine
logger.Debug(dictionary); //This works fine and is indexed and searchable by Stackify 
logger.Debug("Starting some process for client 1", dictionary); //extension method
```

###log4net v1.2.10

**Install via NuGet package**
```
PM> Install-Package StackifyLib.log4net.v1_2_10
```

Note: If you use 1.2.10 then you must use our special nuget package for that version. There is no way to use an assembly redirect because the public key of log4net v1 and v2 are different. Everything else is the same about using log4net with Stackify.


###Direct API

**Install via NuGet package**
```
PM> Install-Package StackifyLib
```

If you use a custom logging framework or a framework not currently supported, you can easily send logs to Stackify with our core library and API like so:

```csharp
StackifyLib.Logger.Queue("DEBUG", "My log message");
StackifyLib.Logger.QueueException("Test exception", new ApplicationException("Sky is falling"));

StackifyLib.Logger.Shutdown(); //should be called before your app closes to flush the log queue
        
//More advanced example
LogMsg msg = new LogMsg();
msg.Ex = StackifyError.New(new ApplicationException("Exception goes here"));
msg.AppDetails = new LogMsgGroup() {AppName = "My app", Env = "Prod", ServerName = Environment.MachineName};
msg.data = StackifyLib.Utils.HelperFunctions.SerializeDebugData(new { color= "red"}, true);
msg.Msg = "My log message";
msg.Level = "ERROR";
StackifyLib.Logger.QueueLogObject(msg);
```

*Make sure you call StackifyLib.Logger.Shutdown() before your app ends to flush the queue*

###Configuring with Azure service definitions

StackifyLib reads the license key, app name, and environment settings from normal web.config appSettings. If you would prefer to store the settings in an [azure cloud deployment cscfg](http://msdn.microsoft.com/en-us/library/azure/hh369931.aspx#NameValue), then you can create a little code to read the settings from there and set the StackifyLib settings in code like this in some similar way.

```csharp
public class MvcApplication : System.Web.HttpApplication
{
        public override void Init()
        {
                base.Init();
                StackifyLib.Logger.GlobalApiKey = GetConfig("Stackify.ApiKey");
                StackifyLib.Logger.GlobalEnvironment = GetConfig("Stackify.Environment");
                StackifyLib.Logger.GlobalAppName = "My App Name"; //probably no reason to make this one configurable
        }
}

public static string GetConfig(string configName)
{
        //Doing an if here in case it's being used outside of azure emulator.
        //Do this however works best for your project
        try
        {
        if (RoleEnvironment.IsAvailable)
        {
                return RoleEnvironment.GetConfigurationSettingValue(configName);
        }
        else
        {
                return ConfigurationManager.AppSettings[configName];
        }
        }
        catch (Exception ex)
        {
        }
        return null;
}
```