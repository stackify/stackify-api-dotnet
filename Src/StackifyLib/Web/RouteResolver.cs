#if NETFULL
using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Routing;

namespace StackifyLib.Web
{
    /// <summary>
    /// Can be used to figure out the MVC route for a web request
    /// </summary>
    public class RouteResolver
    {
        public class Route
        {
            public string Area { get; set; }
            public string Action { get; set; }
            public string Controller { get; set; }
            public string Pattern { get; set; }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                if (!string.IsNullOrEmpty(Area))
                {
                    sb.Append(Area + ".");
                }

                if (!string.IsNullOrEmpty(Controller))
                {
                    sb.Append(Controller + ".");
                }

                if (!string.IsNullOrEmpty(Action))
                {
                    sb.Append(Action);
                }

                return sb.ToString().Trim('.');
            }
        }

        private HttpContext _Context = null;

        Route _Route = new Route();

        public RouteResolver()
            : this(System.Web.HttpContext.Current)
        {
        }

        public RouteResolver(HttpContext context)
        {
            _Context = context;

            _Route = EvalRoute();
        }


        private bool _IISAppended = false;

        public Route GetRoute()
        {
            return _Route;
        }


        /// <summary>
        /// Easy to way to log the MVC route to your IIS log
        /// </summary>
        public void AppendToIISLog()
        {
            try
            {
                if (_IISAppended) return; //don't do this twice

                if (_Context == null) return;

                if (_Context.Items != null && _Context.Items["StackifyAppendToLogSet"] != null) return;

                string routeKey = _Route.ToString();

                if (!string.IsNullOrEmpty(routeKey))
                {
                    _Context.Response.AppendToLog("&ResolvedRoute={" + routeKey.Trim('.').Replace(" ", "-") + "}");
                    _Context.Items["StackifyAppendToLogSet"] = true;
                    _IISAppended = true;
                }
            }
            catch (Exception e)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("Error Appending route to IIS log " + e.ToString());
            }
        }

        public static void AppendToIISLog(HttpContext context, string route)
        {
            if (context == null) return;

            if (context.Items != null && context.Items["StackifyAppendToLogSet"] != null) return;

            if (!string.IsNullOrEmpty(route))
            {
                context.Response.AppendToLog("&ResolvedRoute={" + route.Trim('.').Replace(" ", "-") + "}");
                context.Items["StackifyAppendToLogSet"] = true;
            }
        }

        public void CreateServerVariables()
        {
            if (!string.IsNullOrEmpty(_Route.Pattern))
            {
                _Context.Request.ServerVariables["ROUTE_PATTERN"] = _Route.Pattern;
            }

            if (!string.IsNullOrEmpty(_Route.Area))
            {
                _Context.Request.ServerVariables["ROUTE_AREA"] = _Route.Area;
            }

            if (!string.IsNullOrEmpty(_Route.Controller))
            {
                _Context.Request.ServerVariables["ROUTE_CONTROLLER"] = _Route.Controller;
            }

            if (!string.IsNullOrEmpty(_Route.Action))
            {
                _Context.Request.ServerVariables["ROUTE_ACTION"] = _Route.Action;
            }
        }

        private Route EvalRoute()
        {
            Route route = new Route();

            try
            {
                if (_Context == null) return route;

                var wrapper = new HttpContextWrapper(_Context);

                if (wrapper == null || RouteTable.Routes == null) return route;

                var routeData = RouteTable.Routes.GetRouteData(wrapper);

                if (routeData != null && routeData.Values != null && routeData.Values.Any())
                {
                    if (routeData.Route is System.Web.Routing.Route)
                    {
                        route.Pattern =((System.Web.Routing.Route)(routeData.Route)).Url;
                    }

                    if (routeData.DataTokens != null && routeData.DataTokens.ContainsKey("area"))
                    {
                        route.Area = (string)routeData.DataTokens["area"];
                    }

                    if (routeData.Values != null)
                    {
                        if (string.IsNullOrEmpty(route.Area) && routeData.Values.ContainsKey("area"))
                        {
                            route.Area = (string) routeData.Values["area"];
                        }

                        if (routeData.Values.ContainsKey("controller"))
                        {
                            route.Controller = (string) routeData.Values["controller"];
                        }

                        if (routeData.Values.ContainsKey("action"))
                        {
                            route.Action = (string) routeData.Values["action"];
                        }
                    }
                }
            }
            catch (Exception e)
            {
                StackifyLib.Utils.StackifyAPILogger.Log("Error resolving route " + e.ToString());
            }

            return route;
        }
    }
}
#endif