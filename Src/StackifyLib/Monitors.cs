using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using StackifyLib.Utils;

namespace StackifyLib
{
    /// <summary>
    /// Query Stackify API for monitoring metrics
    /// </summary>
    public class Monitors
    {
        public Models.Device[] GetDeviceList()
        {
            Utils.HttpClient client = new HttpClient(null, null);
            var response = client.SendAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
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

        public Models.Monitor[] GetDeviceMonitors(int clientDeviceId)
        {
            Utils.HttpClient client = new HttpClient(null, null);
            var response = client.SendAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
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

        public Models.MonitorMetric GetMetrics(int monitorID, DateTimeOffset startDateUtc, DateTimeOffset endDateUtc, short pointSizeInMinutes = 5)
        {
            Utils.HttpClient client = new HttpClient(null, null);
            var response = client.SendAndGetResponse(System.Web.VirtualPathUtility.AppendTrailingSlash(client.BaseAPIUrl) +
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
