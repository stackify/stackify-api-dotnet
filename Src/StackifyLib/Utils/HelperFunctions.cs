using System.Collections;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;


namespace StackifyLib.Utils
{
    public class HelperFunctions
    {
        static List<string> _BadTypes = new List<string>() { "log4net.Util.SystemStringFormat", "System.Object[]" };
        static JsonSerializer serializer = new JsonSerializer { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };

        //Trying to serialize something that the user passed in
        public static string SerializeDebugData(object logObject, Dictionary<string, object> properties = null)
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

                    if (logObject is string || t.FullName == "log4net.Util.SystemStringFormat" )
                    {
                        jObject = new JObject();
                        jObject.Add("logArg", new JValue(logObject.ToString()));
                    }
                    else if (t.IsPrimitive || t.BaseType == typeof(ValueType))
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
                    //look for some things we don't want to touch
                    else if (logObject is IDisposable || logObject is MarshalByRefObject)
                    {

                    }
                    else if (!_BadTypes.Contains(t.ToString()))
                    {
                        var token = JToken.FromObject(logObject, serializer);

                        if (token is JObject)
                        {
                            jObject = (JObject)token;
                            var type = logObject.GetType();

                            if (type.IsPrimitive || type.Name == "String" || type.BaseType == typeof(ValueType) || type.Name.Contains("AnonymousType") || type.FullName.Contains("System.Collections.Generic.Dictionary"))
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
                                var array = (Array)logObject;

                                if (array.Length > 0)
                                {
                                    var child = array.GetValue(0);

                                    var childtype = child.GetType();

                                    if (childtype.IsPrimitive || childtype.Name == "String" || childtype.BaseType == typeof(ValueType))
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
                                var genericArgs = type.GetGenericArguments();

                                if (genericArgs.Any())
                                {
                                    var childtype = genericArgs.First();
                                    if (childtype.IsPrimitive || childtype.Name == "String" || childtype.BaseType == typeof(ValueType))
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

                        }
                        else if (token is JValue)
                        {
                            jObject = new JObject();
                            jObject.Add("logArg", token);
                        }
                        else
                        {
                            string x = "what is this";
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
                            props.Add(prop.Key, JObject.FromObject(prop.Value,serializer));
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
                                                           NullValueHandling = NullValueHandling.Ignore
                                                       });
            }

            return null;
        }

        public static bool IsValueType(object obj)
        {
            if (obj == null)
                return false;

            var t = obj.GetType();

            return t.IsPrimitive || t.Equals(typeof(string));
        }


        public static dynamic ToDynamic(object value)
        {
            IDictionary<string, object> expando = new ExpandoObject();

            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(value.GetType()))
                expando.Add(property.Name, property.GetValue(value));

            return expando as ExpandoObject;
        }
    }
}
