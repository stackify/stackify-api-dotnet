using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
#if NETFRAMEWORK
using System.Runtime.Remoting.Messaging;
#endif
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;


namespace StackifyLib.Utils
{
    public class HelperFunctions
    {
        static List<string> _BadTypes = new List<string>() { "log4net.Util.SystemStringFormat", "System.Object[]" };
        static JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter>() {
                    new ToStringConverter("Module", typeof(Module)),
                    new ToStringConverter("Method", typeof(MemberInfo)),
                    new ToStringConverter("Assembly", typeof(Assembly)),
            }
        };
        static JsonSerializer serializer = JsonSerializer.Create(serializerSettings);

        /// <summary>
        /// Trying to serialize something that the user passed in. Sometimes this is used to serialize what we know is additional debug and sometimes it is the primary logged item. This is why the serializeSimpleTypes exists. For additional debug stuff we always serialize it. For the primary logged object we won't because it doesn't make any sense to put a string in the json as well as the main message. It's meant for objects.
        /// </summary>
        /// <param name="logObject"></param>
        /// <param name="serializeSimpleTypes"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public static string SerializeDebugData(object logObject, bool serializeSimpleTypes, Dictionary<string, object> properties = null)
        {
            Type t = null;
            JObject jObject = null;

            try
            {
                if (logObject == null)
                {
                }
                else
                {
                    t = logObject.GetType();
#if NET40
                    var typeInfo = t;
#else
                    var typeInfo = t.GetTypeInfo();
#endif
                    if (logObject is string || t.FullName == "log4net.Util.SystemStringFormat")
                    {
                        if (serializeSimpleTypes)
                        {
                            jObject = new JObject();
                            jObject.Add("logArg", new JValue(logObject.ToString()));
                        }
                    }
                    else if (typeInfo.IsPrimitive || typeInfo.BaseType == typeof(ValueType))
                    {
                        if (serializeSimpleTypes)
                        {
                            jObject = new JObject();
                            try
                            {
                                jObject.Add("logArg", new JValue(logObject));
                            }
                            catch (ArgumentException)
                            {
                                jObject.Add("logArg", new JValue(logObject.ToString()));
                            }
                        }
                    }
                    //look for some things we don't want to touch
                    else if (logObject is IDisposable)// || logObject is MarshalByRefObject)
                    {

                    }
                    else if (!_BadTypes.Contains(t.ToString()))
                    {
                        var token = JToken.FromObject(logObject, serializer);

                        if (token is JObject)
                        {
                            jObject = (JObject)token;
                            var type = logObject.GetType();

                            //do we log the objectType? Not logging it for simple things
                            if (typeInfo.IsPrimitive == false && type.Name != "String" && typeInfo.BaseType != typeof(ValueType) && type.Name.Contains("AnonymousType") == false && (type.FullName == null || type.FullName.Contains("System.Collections.Generic.Dictionary") == false))
                            {
                                jObject.Add("objectType", type.FullName);
                            }
                        }
                        else if (token is JArray)
                        {
                            jObject = new JObject();
                            jObject.Add("logArg", token);

                            var type = logObject.GetType();

                            if (type.IsArray)
                            {
                                var array = (Array) logObject;

                                if (array.Length > 0)
                                {
                                    var child = array.GetValue(0);

                                    var childtype = child.GetType();

#if NET40
                                    var childtypeinfo = childtype;
#else
                                    var childtypeinfo = childtype.GetTypeInfo();
#endif

                                    if (childtypeinfo.IsPrimitive == false && childtype.Name != "String" && childtypeinfo.BaseType != typeof(ValueType))
                                    {
                                        jObject.Add("objectType", childtype.FullName);
                                    }
                                }
                            }
                            else
                            {
                                if (!typeInfo.ContainsGenericParameters)
                                {
                                    jObject.Add("objectType", type.FullName);
                                }
                                else
                                {
#if NETFULL
                                    var genericArgs = typeInfo.GetGenericArguments();
#else
                                    var genericArgs = typeInfo.IsGenericTypeDefinition ?
                                        type.GetTypeInfo().GenericTypeParameters :
                                        type.GetTypeInfo().GenericTypeArguments;
#endif
                                    if (genericArgs != null && genericArgs.Length > 0)
                                    {
                                        var childtype = genericArgs.First();
#if NET40
                                        var childtypeinfo = childtype;
#else
                                        var childtypeinfo = childtype.GetTypeInfo();
#endif
                                        if (childtypeinfo.IsPrimitive == false && childtype.Name != "String" && childtypeinfo.BaseType != typeof(ValueType))
                                        {
                                            jObject.Add("objectType", childtype.FullName);
                                        }
                                    }
                                    else
                                    {
                                        jObject.Add("objectType", type.FullName);
                                    }
                                }
                            }
                        }
                        else if (token is JValue)
                        {
                            if (serializeSimpleTypes)
                            {
                                jObject = new JObject();
                                jObject.Add("logArg", token);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_BadTypes)
                {
                    _BadTypes.Add(t.ToString());
                }
                Utils.StackifyAPILogger.Log(ex.ToString());
            }

            string data = null;
            if (properties != null && properties.Count > 0)
            {

                if (jObject == null)
                {
                    jObject = new JObject();
                }

                JObject props = new JObject();
                foreach (var prop in properties)
                {
                    try
                    {
                        if (IsValueType(prop.Value))
                        {
                            props.Add(prop.Key, new JValue(prop.Value));
                        }
                        else
                        {
                            props.Add(prop.Key, JObject.FromObject(prop.Value, serializer));
                        }

                    }
                    catch (Exception ex)
                    {
                        StackifyAPILogger.Log(ex.ToString());
                    }

                }

                jObject.Add("context", props);

            }

            if (jObject != null)
            {

                jObject = GetPrunedObject(jObject, Config.LoggingJsonMaxFields);

                return JsonConvert.SerializeObject(jObject, serializerSettings);
            }

            return null;
        }

        /// <summary>
        ///     If the <see cref="JObject"/> provided has move fields than maxFields
        ///     will return a simplified <see cref="JObject"/> with original as an unparsed string message,
        ///     otherwise will return original <see cref="JObject"/>
        /// </summary>
        private static JObject GetPrunedObject(JObject obj, int maxFields)
        {
            var fieldCount = GetFieldCount(obj);

            if (fieldCount > maxFields)
            {
                return new JObject
                {
                    { "invalid", true },
                    { "message", obj.ToString() }
                };
            }

            return obj;
        }

        private static int GetFieldCount(JToken obj)
        {
            switch (obj.Type)
            {
                case JTokenType.Array:
                case JTokenType.Object:
                    return obj.Children().Sum(i => GetFieldCount(i));
                case JTokenType.Property:
                    return GetFieldCount(obj.Value<JProperty>().Value);
                default:
                    return 1;
            }
        }

        public static bool IsValueType(object obj)
        {
            if (obj == null)
                return false;

            var t = obj.GetType();
#if NET40
            return t.IsPrimitive || t.Equals(typeof(string));
#else
            return t.GetTypeInfo().IsPrimitive || t.Equals(typeof(string));
#endif
        }

        public static string CleanPartialUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.Contains("/"))
                return url;

            string[] urlPieces = url.Split(new char[] { '/' });

            var sbNewUrl = new StringBuilder(url.Length);

            int index = 0;
            foreach (string piece in urlPieces)
            {
                if (string.IsNullOrEmpty(piece))
                {
                    continue;
                }

                long val;
                Guid guidval;
                if (long.TryParse(piece, out val))
                {
                    sbNewUrl.Append("/{id}");
                }
                else if (Guid.TryParse(piece, out guidval))
                {
                    sbNewUrl.Append("/{guid}");
                }
                else
                {
                    sbNewUrl.AppendFormat("/{0}", piece);
                }

                index++;
            }

            if (url.EndsWith("/"))
            {
                sbNewUrl.Append("/");
            }

            return sbNewUrl.ToString();
        }
        public static string GetRequestId()
        {
            string reqId = null;

#if NETFULL
            try
            {
                if (string.IsNullOrEmpty(reqId))
                {
                    var stackifyRequestID = CallContext.LogicalGetData("Stackify-RequestID");

                    if (stackifyRequestID != null)
                    {
                        reqId = stackifyRequestID.ToString();
                    }
                }

                if (string.IsNullOrEmpty(reqId))
                {
                    //gets from Trace.CorrelationManager.ActivityId but doesnt assume it is guid since it technically doesn't have to be
                    //not calling the CorrelationManager method because it blows up if it isn't a guid
                    var correltionManagerId = CallContext.LogicalGetData("E2ETrace.ActivityID");

                    if (correltionManagerId != null && correltionManagerId is Guid &&
                        ((Guid) correltionManagerId) != Guid.Empty)
                    {
                        reqId = correltionManagerId.ToString();
                    }
                }
            }
            catch (System.Web.HttpException ex)
            {
                StackifyAPILogger.Log("Request not available \r\n" + ex.ToString());
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Error figuring out TransID \r\n" + ex.ToString());
            }
#endif

            try
            {
                if (string.IsNullOrEmpty(reqId))
                {
                    reqId = getContextProperty("RequestId") as string;
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Error figuring out TransID \r\n" + ex.ToString());
            }

            return reqId;
        }

        public static string GetReportingUrl()
        {
            return GetStackifyProperty("REPORTING_URL");
        }

        public static string GetAppName()
        {
            if (!IsBeingProfiled)
            {
                return Config.AppName;
            }

            // Getting profiler app name and environment are a side effect of checking the profiler wrapper
            GetWrapperAssembly();

            return _profilerAppName ?? Config.AppName;
        }

        public static string GetAppEnvironment()
        {
            if (!IsBeingProfiled)
            {
                return Config.AppName;
            }

            // Getting profiler app name and environment are a side effect of checking the profiler wrapper
            GetWrapperAssembly();

            return _profilerEnvironment ?? Config.Environment;
        }

        private static Assembly _wrapperAssembly = null;
        private static Type _stackifyCallContextType = null;

        protected static Assembly GetWrapperAssembly()
        {
            if (!IsBeingProfiled)
            {
                return null;
            }

            if (_wrapperAssembly != null)
            {
                return _wrapperAssembly;
            }

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                var agentAssemblyQry = assemblies.Where(assembly => assembly.FullName.StartsWith("Stackify.Agent,"));

                foreach (var middleware in agentAssemblyQry)
                {
                    var stackifyCallContextType = middleware?.GetType("Stackify.Agent.Threading.StackifyCallContext");

                    if (stackifyCallContextType != null)
                    {
                        GetProfilerAppNameAndEnvironment(middleware);

                        _stackifyCallContextType = stackifyCallContextType;
                        _wrapperAssembly = middleware;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return _wrapperAssembly;
        }

        private static string _profilerAppName = null;
        private static string _profilerEnvironment = null;

        private static void GetProfilerAppNameAndEnvironment(Assembly middleware)
        {
            try
            {
                // Get AppName and Environment and cache them, so we won't query over and over.
                var agentConfigType = middleware?.GetType("Stackify.Agent.Configuration.AgentConfig");

                if (agentConfigType != null)
                {
                    var profilerSettings = agentConfigType.GetProperty("Settings")?.GetValue(null, null);

                    if (profilerSettings != null)
                    {
                        var settingsType = profilerSettings.GetType();

                        _profilerAppName = settingsType?.GetProperty("AppName")?.GetValue(profilerSettings, null) as string;
                        _profilerEnvironment = settingsType?.GetProperty("Environment")?.GetValue(profilerSettings, null) as string;
                    }
                }
            }
            catch
            {
                // Ignore errors here
            }
        }

        private static object getContextProperty(string propName)
        {
            if (!IsBeingProfiled || string.IsNullOrWhiteSpace(propName))
            {
                return null;
            }

            if (_wrapperAssembly == null)
            {
                GetWrapperAssembly();
            }

            try
            {
                if (_stackifyCallContextType != null)
                {
                    var traceContextProp = _stackifyCallContextType.GetProperty("TraceContext")?.GetValue(null, null);
                    if (traceContextProp != null)
                    {
                        var traceContextType = traceContextProp.GetType();
                        var contextProp = traceContextType.GetProperty(propName);
                        var propValue =  contextProp?.GetValue(traceContextProp, null);
                        return propValue;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        private static string GetStackifyProperty(string propName)
        {
            if (!IsBeingProfiled || string.IsNullOrWhiteSpace(propName))
            {
                return null;
            }

            try
            {
                var stackifyProps = getContextProperty("props");

                if (stackifyProps != null)
                {
                    var propsType = stackifyProps.GetType();

                    var getItemMethod = propsType.GetMethod("get_Item", new[] {typeof(string)});

                    if (getItemMethod != null)
                    {
                        return getItemMethod.Invoke(stackifyProps, new[] {propName}) as string;
                    }
                }
            }
            catch
            {
                // Ignore Errors
            }

            return null;
        }

        private static Boolean? _isBeingProfiled = null;

        protected static bool IsBeingProfiled
        {
            get
            {
                if (_isBeingProfiled.HasValue)
                {
                    return _isBeingProfiled.Value;
                }

#if NETFRAMEWORK
                var profilerEnv = "COR_ENABLE_PROFILING";
                var profilerUuidEnv = "COR_PROFILER";
#else
                string profilerEnv = null;
                string profilerUuidEnv = null;

                // .Net Standard could be .Net Core or .Net Framework, so check
                var framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

                if (string.IsNullOrEmpty(framework))
                {
                    return false;
                }

                if (framework.StartsWith(".NET Native", StringComparison.OrdinalIgnoreCase))
                {
                    // Native code can't be profiled
                    return false;
                }
                else if (framework.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
                {
                    profilerEnv = "COR_ENABLE_PROFILING";
                    profilerUuidEnv = "COR_PROFILER";
                }
                else // Assume everything else is .Net Core current values would be .Net Core, .Net 5.x and .Net 6.x
                {
                    profilerEnv = "CORECLR_ENABLE_PROFILING";
                    profilerUuidEnv = "CORECLR_PROFILER";
                }

                if (profilerEnv == null || profilerUuidEnv == null)
                {
                    // This code should be unreachable, but just in case the above checks are changed we handle it
                    return false;
                }
#endif

                var enableString = Environment.GetEnvironmentVariable(profilerEnv);
                var uuidString = Environment.GetEnvironmentVariable(profilerUuidEnv);

                if (!string.IsNullOrWhiteSpace(enableString) &&
                    !string.IsNullOrWhiteSpace(uuidString))
                {
                    _isBeingProfiled = string.Equals("1", enableString?.Trim())
                        && string.Equals("{cf0d821e-299b-5307-a3d8-b283c03916da}", uuidString.Trim(), StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    _isBeingProfiled = false;
                }

                return _isBeingProfiled.Value;
            }
        }

        public static String MaskReportingUrl(String url)
        {
            String maskedUrl = "";

            try
            {
                String[] pathFields = url.Split('/');

                List<String> stripFields = new List<String>(pathFields.Length);

                foreach (String field in pathFields)
                {
                    stripFields.Add(Mask(field));
                }

                maskedUrl = string.Join("/", stripFields);

                if (maskedUrl.EndsWith("/"))
                {
                    maskedUrl = maskedUrl.Substring(0, maskedUrl.Length - 1);
                }
            }
            catch
            {
                // If we had errors, just return what we got.
                return url;
            }

            return maskedUrl;
        }


        private static readonly Regex ID_REGEX = new Regex("^(\\d+)$", RegexOptions.Compiled);

        private static readonly Regex GUID_REGEX =
            new Regex("^(?i)(\\b[A-F0-9]{8}(?:-[A-F0-9]{4}){3}-[A-F0-9]{12}\\b)$", RegexOptions.Compiled);

        private static readonly Regex EMAIL_REGEX =
            new Regex(
                "^((([!#$%&'*+\\-/=?^_`{|}~\\w])|([!#$%&'*+\\-/=?^_`{|}~\\w][!#$%&'*+\\-/=?^_`{|}~\\.\\w]{0,}[!#$%&'*+\\-/=?^_`{|}~\\w]))[@]\\w+([-.]\\w+)*\\.\\w+([-.]\\w+)*)$",
                RegexOptions.Compiled);

        private static readonly Regex IP_REGEX =
            new Regex(
                "^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
                RegexOptions.Compiled);

        private static String Mask(String field)
        {

            if (ID_REGEX.IsMatch(field))
            {
                return "{id}";
            }

            if (GUID_REGEX.IsMatch(field))
            {
                return "{guid}";
            }

            if (EMAIL_REGEX.IsMatch(field))
            {
                return "{email}";
            }

            if (IP_REGEX.IsMatch(field))
            {
                return "{ip}";
            }

            if (field.Contains(';'))
            {
                return field.Substring(0, field.IndexOf(';'));
            }

            return field;
        }
    }

    public class ToStringConverter : JsonConverter
    {
        private string _propName = "";
        private Type _type;

        public ToStringConverter(string propName, Type t)
        {
            _propName = propName;
            _type = t;
        }

        public override bool CanConvert(Type objectType)
        {
            return _type.IsAssignableFrom(objectType);
        }

        public override bool CanRead => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value != null)
            {
                var o = new JObject();

                o.Add(new JProperty(_propName, value.ToString()));

                o.WriteTo(writer);
            }
        }
    }
}
