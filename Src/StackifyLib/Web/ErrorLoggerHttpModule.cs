#if NET451 || NET45 || NET40
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace StackifyLib.Web
{
    public class ErrorLoggerHttpModule : IHttpModule
    {
        void IHttpModule.Init(HttpApplication context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            context.Error += context_Error;
        }

        void context_Error(object sender, EventArgs e)
        {
            try
            {
                var application = (HttpApplication)sender;
                Logger.QueueException("Unhandled Web Exception", application.Server.GetLastError());
            }
            catch (Exception)
            {
                
            }
        }

        void IHttpModule.Dispose()
        {
            
        }
    }
}
#endif