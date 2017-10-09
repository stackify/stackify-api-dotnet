using System.Collections;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;


namespace StackifyLib.Utils
{
    public class HelperFunctions
    {
        static List<string> _BadTypes = new List<string>() { "log4net.Util.SystemStringFormat", "System.Object[]" };
        static JsonSerializer serializer = new JsonSerializer { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };

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
      //      TypeInfo typeInfo = null;
            JObject jObject = null;

            try
            {
                if (logObject == null)
                {
                }
                else
                {
                    t = logObject.GetType();
                    var typeInfo = t.GetTypeInfo();
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
                            if (typeInfo.IsPrimitive || type.Name == "String" || typeInfo.BaseType == typeof(ValueType) || type.Name.Contains("AnonymousType") || type.FullName.Contains("System.Collections.Generic.Dictionary"))
                            {

                            }
                            else
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

                                    var childtypeinfo = childtype.GetTypeInfo();

                                    if (childtypeinfo.IsPrimitive || childtype.Name == "String" ||
                                        childtypeinfo.BaseType == typeof (ValueType))
                                    {

                                    }
                                    else
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
#if NETFULL
                                else
                                {

                                    var genericArgs = typeInfo.GetGenericArguments();

                                    if (genericArgs.Any())
                                    {
                                        var childtype = genericArgs.First();

                                        var childtypeinfo = childtype.GetTypeInfo();

                                        if (childtypeinfo.IsPrimitive || childtype.Name == "String" ||
                                            childtypeinfo.BaseType == typeof (ValueType))
                                        {

                                        }
                                        else
                                        {
                                            jObject.Add("objectType", childtype.FullName);
                                        }
                                    }
                                    else
                                    {
                                        jObject.Add("objectType", type.FullName);
                                    }
                                }
#endif
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
            if (properties != null && properties.Any())
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
                return JsonConvert.SerializeObject(jObject,
                                                   new JsonSerializerSettings()
                                                   {
                                                       NullValueHandling = NullValueHandling.Ignore,
                                                       ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                                                   });
            }

            return null;
        }

        public static bool IsValueType(object obj)
        {
            if (obj == null)
                return false;

            var t = obj.GetType();
            return t.GetTypeInfo().IsPrimitive || t.Equals(typeof(string));
        }


        //public static dynamic ToDynamic(object value)
        //{
        //    IDictionary<string, object> expando = new ExpandoObject();

        //    foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(value.GetType()))
        //        expando.Add(property.Name, property.GetValue(value));

        //    return expando as ExpandoObject;
        //}


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
    }
}
