using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Serilog;
using StackifyLib;
using StackifyLib.CoreLogger;

namespace CoreWebApp
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            StackifyLib.Utils.StackifyAPILogger.OnLogMessage += StackifyAPILogger_OnLogMessage;
            StackifyLib.Utils.StackifyAPILogger.LogEnabled = true;

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();

            StackifyLib.Config.Environment = env.EnvironmentName;
        }

        private void StackifyAPILogger_OnLogMessage(string data)
        {
           Debug.WriteLine(data);
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            // Add framework services.
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        /// <param name="loggerFactory"></param>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            ApplicationLogging.ConfigureLogger(loggerFactory);
            ApplicationLogging.LoggerFactory = loggerFactory;

            //Debug.WriteLine(Environment.StackTrace);
            //loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            //loggerFactory.AddDebug(LogLevel.None);
            //loggerFactory.AddFile("mylogfile.log", LogLevel.Debug);
            ////loggerFactory.ConfigureNLog("nlog.config");
            ////      app.ApplicationServices.GetService()
            //loggerFactory.AddStackify();
          //  loggerFactory.AddNLog();
        //    loggerFactory.AddSerilog();
            
            // var path = Path.Combine(Directory.GetCurrentDirectory(), "nlog.config");
            //  NLog.LogManager.Configuration = new XmlLoggingConfiguration(path, true);
        //    app.AddNLogWeb();
            app.ConfigureStackifyLogging(Configuration);


            app.UseMvc();
        }
    }
}
