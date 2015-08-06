using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using StackifyLib.Utils;

namespace StackifyLib
{
    public class API
    {
        private static Utils.HttpClient client;
        static API()
        {
         client = new HttpClient(null, null);   
        }

        public static Models.Device[] GetDeviceList()
        {
         
            var response = client.SendJsonAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
                                      "API/Device/GetAll",
                                       null);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return JsonConvert.DeserializeObject<Models.Device[]>(response.ResponseText);
            }
            else
            {
                return null;
            }
        }

        public static bool RemoveServerByID(int id, bool uninstallAgent = false)
        {
            var response = client.POSTAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
                                      "API/Device/RemoveServerByID/?id=" + id + "&uninstallAgent=" + uninstallAgent, null);

            return response.StatusCode == HttpStatusCode.OK;
        }

        public static bool RemoveServerByName(string name, bool uninstallAgent = false)
        {
            var response = client.POSTAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
                                      "API/Device/RemoveServerByName/?name=" + name + "&uninstallAgent=" + uninstallAgent,
                                       null);

            return response.StatusCode == HttpStatusCode.OK;
        }

        public static bool UninstallAgentByID(int id)
        {
            var response = client.POSTAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
                                      "API/Device/UninstallAgentByID/?id=" + id,
                                       null);

            return response.StatusCode == HttpStatusCode.OK;
        }

        public static bool UninstallAgentByName(string name)
        {
            var response = client.POSTAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
                                      "API/Device/UninstallAgentByName/?name=" + name,
                                       null);

            return response.StatusCode == HttpStatusCode.OK;
        }

        public static bool? ConfigureAlerts(Dictionary<int, string> devices)
        {
            var response = client.SendJsonAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
                                      "API/Device/ConfigureAlerts",
                                       JsonConvert.SerializeObject(devices));

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return JsonConvert.DeserializeObject<bool>(response.ResponseText);
            }
            else
            {
                return null;
            }
        }

        public static Models.Monitor[] GetDeviceMonitors(int clientDeviceId)
        {
            var response = client.SendJsonAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
                                      "API/Device/Monitors/" + clientDeviceId.ToString(),
                                       null);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return JsonConvert.DeserializeObject<Models.Monitor[]>(response.ResponseText);
            }
            else
            {
                return null;
            }
        }

        public static Models.MonitorMetric GetMetrics(int monitorID, DateTimeOffset startDateUtc, DateTimeOffset endDateUtc, short pointSizeInMinutes = 5)
        {
            var response = client.SendJsonAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
                                      "API/Device/MonitorMetrics/" + string.Format("?monitorID={0}&startDateUtc={1}&endDateUtc={2}&pointSizeInMinutes={3}", monitorID, startDateUtc.UtcDateTime.ToString("o"), endDateUtc.UtcDateTime.ToString("o"), pointSizeInMinutes),
                                       null);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return JsonConvert.DeserializeObject<Models.MonitorMetric>(response.ResponseText);
            }
            else
            {
                return null;
            }
        }
    }
}
