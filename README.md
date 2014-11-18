#Stackify API for .NET

Library for Stackify users to integrate Stackify in to their projects. Provides support for sending errors, logs, and custom metrics to Stackify. Also some support for querying metric data back out of Stackify for use in external projects.

- [Homepage](http://www.stackify.com)
- [Documentation](http://docs.stackify.com/s/3095/m/7787)
- [NuGet Package](https://www.nuget.org/packages?q=Stackify)

#Basics

Several nuget packages are available for Stackify's core API as well as various logging frameworks. Please install the proper packages. All of the available packages for various logging frameworks are wrapper on top of StackifyLib which can also be used directly for logging. 

Features

 - Asynchronous logging framework for high performance
 - Automatic queueing and error fall back logic 
 - Support for NLog, log4net, ELMAH, and direct API usage
 - Can be plugged in to custom built or other logging frameworks
 - Does not depend on the Stackify monitoring agent. All data is sent directly to Stackify.


The following is required in your App.config or Web.config:

        <appSettings>
            <add key="Stackify.ApiKey" value="YOUR API KEY HERE" />
            <add key="Stackify.AppName" value="YOUR APP NAME" />
            <add key="Stackify.Environment" value="OPTIONAL ENVIRONMENT NAME LIKE PROD, DEV" />
        </appSettings>

Optionally, you can set the config settings in code like so which will override the appSettings configs as well.

        StackifyLib.Logger.GlobalApiKey = "";
        StackifyLib.Logger.GlobalAppName = "";
        StackifyLib.Logger.GlobalEnvironment = "";
        
By default the library will use the WebRequest.DefaultWebProxy. If you want to set a specific proxy server only for StackifyLib, you can do so in code OR via config.

        <appSettings>
                <add key="Stackify.ProxyServer" value="http://test:test@172.19.1.1:8888/" />
        </appSettings>
        
        StackifyLib.Utils.HttpClient.CustomWebProxy = new WebProxy();

If you are having problems you can get logging out of the framework by hooking in to it's custom logging.

        StackifyLib.Utils.StackifyAPILogger.LogEnabled = true;
        StackifyLib.Utils.StackifyAPILogger.OnLogMessage += StackifyAPILogger_OnLogMessage;

        static void StackifyAPILogger_OnLogMessage(string data)
        {
            Debug.WriteLine(data);
        }

Please note that Newtonsoft.Json is used by StackifyLib but is embedded as a resource to avoid version conflicts. Costura.Fody is being used to embed it. If you have any issues with Newtonsoft.Json as a result of using StackifyLib please contact Stackify support.

#Error and Logging

If you log an object with the message, Stackify's log viewer makes it easy to search by these parameters. You can always search by the text in the log message itself, but searching by the logged properties provides a lot more power. If you always logged a "clientid" for example on every log message, you could search in Stackify for "json.clientid:1" and quickly see all logs and errors affecting that specific client. Another big difference and advantage to logging objects is you can do a range type search "json.clientid:[1 TO 10]" which would not be possible by a straight text search.


##NLog 2.0.1.2 - v3.1+


Nuget packages are compiled against 2.0.1.2 but any newer version (including v3) will work with a valid assembly binding redirect.
  
       <dependentAssembly>
        <assemblyIdentity name="NLog" publicKeyToken="5120e14c03d0593c" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-3.1.0.0" newVersion="3.1.0.0" />
      </dependentAssembly>

Sample config:

      <nlog>
        <extensions>
          <add assembly="StackifyLib.nLog"/>
        </extensions>
        <targets>
          <target name="stackify" type="StackifyTarget" globalContextKeys="examplekey1,key2" 
                mappedContextKeys="" callContextKeys="" logMethodNames="true" />
        </targets>
        <rules>
          <logger name="*" writeTo="stackify" minlevel="Debug" />
        </rules>
      </nlog>

Logging custom objects is supported and will be searchable in Stackify's log viewer

        static NLog.Logger nlog = NLog.LogManager.GetCurrentClassLogger();
        Dictionary<string, object> dictionary = new Dictionary<string, object>();
        dictionary["clientid"] = 1;
        dictionary["color"] = "red";
        nlog.Debug("Test message", dictionary);

Options

- GlobalContext and MappedContext keys are fully supported by setting the parameters in the config as a comma delimited list of keys. See sample config above.
- CallContextKeys is an additional feature unrelated to NLog that uses the local thread storage for more advanced tracking of context variables. It is used via CallContext.LogicalSetData(key, value). Research LogicalSetData online to learn more. It is supposed to work better across child Task objects and with async.
- logMethodNames - Method names will show up in the StackifyLog viewer most of the time as the class name that did the logging. For exceptions it will usually show the method name. To enable the exact method name for all logging, set this property to true. Note that it does cause a small performance hit due to walking the StackTrace.

##log4net v2.0+ (v1.2.11+)
  Note: Nuget packages are compiled against 2.0.0 (1.2.11) but any newer version will work with a valid assembly binding redirect. log4net 2.0.3 is actually 1.2.13 which makes the binding redirect look strange.

      <dependentAssembly>
        <assemblyIdentity name="log4net" publicKeyToken="669e0ddf0bb1aa2a" culture="neutral" />
        <bindingRedirect oldVersion="1.2.11.0-1.2.13.0" newVersion="1.2.13.0" />
      </dependentAssembly>

Sample config:

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

Options

- GlobalContext, ThreadContext, and LogicalThreadContext keys are fully supported by setting the parameters in the config as a comma delimited list of keys. See sample config above.
- CallContextKeys is an additional feature unrelated to log4net that uses the local thread storage for more advanced tracking of context variables. LogicalThreadContext provides the same functionality but uses an internal property collection. We have seen instances where the serialization of that collection can cause exceptions. This was created as an alternative method to the built in function. It is used via CallContext.LogicalSetData(key, value). Research LogicalSetData online to learn more. It is supposed to work better across child Task objects and with async.
- logMethodNames - Method names will show up in the StackifyLog viewer most of the time as the class name that did the logging. For exceptions it will usually show the method name. To enable the exact method name for all logging, set this property to true. Note that it does cause a small performance hit due to walking the StackTrace.


###log4net Extension Methods

log4net does not internally have methods for logging a log message along with an object. Stackify's appenders work fine if you log an object directly or we have created some friendly extension methods to make it easy to log an object with your message at the same time.

        using StackifyLib; //extension methods are here
        static log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Program));
        Dictionary<string, object> dictionary = new Dictionary<string, object>();
        dictionary["clientid"] = 1;
        dictionary["name"] = "process name";
        logger.Debug("Starting some process for client 1"); //Normal basic log message works fine
        logger.Debug(dictionary); //This works fine and is indexed and searchable by Stackify 
        logger.Debug("Starting some process for client 1", dictionary); //extension method


###log4net v1.2.10
  Note: If you use 1.2.10 then you must use our special nuget package for that version. There is no way to use an assembly redirect because the public key of log4net v1 and v2 are different. Everything else is the same about using log4net with Stackify.


##Direct API

If you use a custom logging framework or a framework not currently supported, you can easily send logs to Stackify with our core library and API like so:

        StackifyLib.Logger.Queue("DEBUG", "My log message");
        StackifyLib.Logger.QueueException("Test exception", new ApplicationException("Sky is falling"));

##Configuring with Azure service definitions

StackifyLib reads the license key, app name, and environment settings from normal web.config appSettings. If you would prefer to store the settings in an azure cloud deployment cscfg, then you can create a little code to read the settings from there and set the StackifyLib settings in code like this in some similar way.

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
