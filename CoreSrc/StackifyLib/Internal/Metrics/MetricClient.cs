using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib.Internal.Metrics
{
    public static class MetricClient
    {
        private static HttpClient _httpClient = new HttpClient(null, null);

        private readonly static ConcurrentQueue<Metric> _MetricQueue;

        private static System.Threading.Timer _Timer;

        private static ConcurrentDictionary<string, GetMetricResponse> _MontorIDList = new ConcurrentDictionary<string, GetMetricResponse>();
        private static ConcurrentDictionary<string, MetricAggregate> _AggregateMetrics = new ConcurrentDictionary<string, MetricAggregate>();
        private static ConcurrentDictionary<string, MetricAggregate> _LastAggregates = new ConcurrentDictionary<string, MetricAggregate>();
        private static ConcurrentDictionary<string, MetricSetting> _MetricSettings = new ConcurrentDictionary<string, MetricSetting>();

        private static bool _StopRequested = false;

        static MetricClient()
        {
            _MetricQueue = new ConcurrentQueue<Metric>();
            _Timer = new Timer(UploadMetricsCheck, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public static int QueueSize
        {
            get { return _MetricQueue.Count; }
        }

        /// <summary>
        /// Used to make sure we report 0 values if nothing new comes in
        /// </summary>
        public static void HandleZeroReports(DateTime currentMinute)
        {
            foreach (var item in _LastAggregates)
            {
                MetricSetting setting;
                if (_MetricSettings.TryGetValue(item.Value.NameKey, out setting))
                {
                    if (setting == null)
                    {
                        MetricSetting remove;
                        _MetricSettings.TryRemove(item.Value.NameKey, out remove);
                        continue;
                    }

                    MetricAggregate agg = new MetricAggregate(item.Value.Category, item.Value.Name, item.Value.MetricType);
                    agg.OccurredUtc = currentMinute;


                    switch (item.Value.MetricType)
                    {
                        case MetricType.Counter:
                            setting.AutoReportLastValueIfNothingReported = false;//do not allow this
                            break;
                        case MetricType.CounterTime:
                            setting.AutoReportLastValueIfNothingReported = false; //do not allow this
                            break;
                        case MetricType.MetricAverage:
                            break;
                        case MetricType.MetricLast:
                            break;
                    }

                    if (setting.AutoReportZeroIfNothingReported)
                    {
                        agg.Count = 1;
                        agg.Value = 0;

                    }
                    else if (setting.AutoReportLastValueIfNothingReported)
                    {
                        agg.Count = item.Value.Count;
                        agg.Value = item.Value.Value;
                    }
                    else
                    {
                        continue;
                    }

                    string aggKey = agg.AggregateKey();

                    if (!_AggregateMetrics.ContainsKey(aggKey))
                    {
                        agg.NameKey = item.Value.NameKey;
                        StackifyAPILogger.Log("Creating 0 default value for " + aggKey);
                        _AggregateMetrics[aggKey] = agg;
                    }

                }
            }
        }

        public static List<LatestAggregate> GetLatestMetrics()
        {
            var latest = new List<LatestAggregate>();

            foreach (var item in _LastAggregates)
            {
                var findVal = item.Value;

                LatestAggregate agg = new LatestAggregate();
                agg.Count = findVal.Count;
                agg.MetricType = findVal.MetricType;
                agg.MetricID = findVal.MonitorID;
                agg.Name = findVal.Name;
                agg.OccurredUtc = findVal.OccurredUtc;
                agg.Value = findVal.Value;
                agg.Count = findVal.Count;
                agg.Category = findVal.Category;
                latest.Add(agg);
            }

            return latest;
        }


        public static LatestAggregate GetLatestMetric(string category, string metricName)
        {
            foreach (var item in _LastAggregates.Where(x=>x.Value.Category.Equals(category, StringComparison.OrdinalIgnoreCase) && x.Value.Name.Equals(metricName, StringComparison.OrdinalIgnoreCase)))
            {
                var findVal = item.Value;

                LatestAggregate agg = new LatestAggregate();
                agg.Count = findVal.Count;
                agg.MetricType = findVal.MetricType;
                agg.MetricID = findVal.MonitorID;
                agg.Name = findVal.Name;
                agg.OccurredUtc = findVal.OccurredUtc;
                agg.Value = findVal.Value;
                agg.Count = findVal.Count;
                agg.Category = findVal.Category;
                return agg;
            }

            return null;
        }

        public static void QueueMetric(Metric metric)
        {
            try
            {
                //set a sanity cap
                if (_MetricQueue.Count < 100000)
                {
                    _MetricQueue.Enqueue(metric);
                }
            }
            catch(Exception ex)
            {
                Utils.StackifyAPILogger.Log(ex.ToString());
            }
        }

        private static void Aggregate(MetricAggregate aggregate)
        {
            try
            {
                string aggKey = aggregate.AggregateKey();

                MetricAggregate agg;
                if (!_AggregateMetrics.TryGetValue(aggKey, out agg))
                {

                    if (_AggregateMetrics.Count > 1000)
                    {
                        Utils.StackifyAPILogger.Log("No longer aggregating new metrics because more than 1000 are queued");
                        return;
                    }

                    StackifyAPILogger.Log("Creating aggregate for " + aggKey);
                    _AggregateMetrics[aggKey] = aggregate;
                    agg = aggregate;
                }

                if (aggregate.MetricType == MetricType.MetricLast)
                {
                    agg.Count = 1;
                    agg.Value = aggregate.Value;
                }
                else 
                {
                    agg.Count += aggregate.Count;
                    agg.Value += aggregate.Value;
                }
                _AggregateMetrics[aggKey] = agg;

            }
            catch (Exception ex)
            {

                StackifyAPILogger.Log("Error in StackifyLib with aggregating metrics");
            }
        }


        /// <summary>
        /// Read everything in the queue up to a certain time point
        /// </summary>
        private static void ReadAllQueuedMetrics()
        {
            DateTime maxDate = DateTime.UtcNow; //read only up until now so it doesn't get stuck in an endless loop
                                                //Loop through add sum up the totals of the counts and values by aggregate key then pass it all in at once to update the aggregate dictionary so it is done in one pass

            //key is the aggregate key which is the metric name, type and rounded minute of the occurrence

            StackifyAPILogger.Log("ReadAllQueuedMetrics " + maxDate);

            var batches = new Dictionary<string, MetricAggregate>();

            long processed = 0;

            Metric metric;
            while (_MetricQueue.TryDequeue(out metric))
            {
                processed++;
                metric.CalcAndSetAggregateKey();

                if (!batches.ContainsKey(metric.AggregateKey))
                {
                    string nameKey = metric.CalcNameKey();

                    if (metric.IsIncrement && _LastAggregates.ContainsKey(nameKey))
                    {
                        //if wanting to do increments we need to grab the last value so we know what to increment
                        metric.Value = _LastAggregates[nameKey].Value;
                    }

                    batches[metric.AggregateKey] = new MetricAggregate(metric);

                    //if it is null don't do anything
                    //we are doing it where the aggregates are created so we don't do it one very single metric, just once per batch to optimize performance
                    if (metric.Settings != null)
                    {
                        _MetricSettings[nameKey] = metric.Settings;
                    }

                }

                batches[metric.AggregateKey].Count++;

                if (metric.IsIncrement)
                {
                    //add or subtract
                    batches[metric.AggregateKey].Value += metric.Value;

                    if (metric.Settings != null && batches[metric.AggregateKey].Value < 0 && !metric.Settings.AllowNegativeGauge)
                    {
                        batches[metric.AggregateKey].Value = 0;
                    }

                }
                else if (metric.MetricType == MetricType.MetricLast)
                {
                    //should end up the last value
                    batches[metric.AggregateKey].Value = metric.Value;
                }
                else
                {
                    batches[metric.AggregateKey].Value += metric.Value;    
                }

                if (metric.Occurred > maxDate)
                {
                    //we don't need anything more this recent so bail
                    break;
                }
            }

            StackifyLib.Utils.StackifyAPILogger.Log(string.Format("Read queued metrics processed {0} for max date {1}", processed, maxDate));

            foreach (var batch in batches)
            {
                Aggregate(batch.Value);
            }
        }

        private static void UploadMetricsCheck(object state)
        {
            StackifyAPILogger.Log("Upload metrics check");

            _Timer.Change(-1, -1);

            double seconds = 2; //read quickly in case there is a very high volume to keep queue size down

            if (!_StopRequested)
            {
                bool allSuccess = false;
                DateTime purgeOlderThan = DateTime.UtcNow.AddMinutes(-10);

                DateTime currentMinute = DateTime.UtcNow.Floor(TimeSpan.FromMinutes(1));

                StackifyAPILogger.Log("Calling UploadMetrics " + currentMinute);
                allSuccess = UploadMetrics(currentMinute);

                PurgeOldMetrics(purgeOlderThan);

                if (_AggregateMetrics.Count > 0 && allSuccess)
                {
                    seconds = .1;
                }
            }
            else
            {
                StackifyAPILogger.Log("Metrics processing canceled because stop was requested");
            }



            _Timer.Change(TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(seconds));
     
        }

        
        public static void StopMetricsQueue(string reason = "Unknown")
        {
            StackifyAPILogger.Log("StopMetricsQueue called by " + reason, true);
            
            //don't let t his method run more than once
            if (_StopRequested)
                return;

            _StopRequested = true;

            try
            {
                DateTime currentMinute = DateTime.UtcNow.AddMinutes(2).Floor(TimeSpan.FromMinutes(1));

                UploadMetrics(currentMinute);

            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("StopMetricsQueue error" + ex.ToString(), true);
            }


            _StopRequested = false;
            StackifyAPILogger.Log("StopMetricsQueue completed" + reason, true);
        }

        public static bool UploadMetrics(DateTime currentMinute)
        {
            bool success = false;
             List<KeyValuePair<string, MetricAggregate>> metrics = new List<KeyValuePair<string, MetricAggregate>>();
            try
            {
                //read everything up to now
                ReadAllQueuedMetrics();

                //ensures all the aggregate keys exists for any previous metrics so we report zeros on no changes
                HandleZeroReports(currentMinute);

                
                var getForRecent = _AggregateMetrics.Where(x => x.Value.OccurredUtc < currentMinute && x.Value.OccurredUtc > DateTime.UtcNow.AddMinutes(-5)).Select(x=>x.Value).ToList();

                SetLatestAggregates(getForRecent);


                if (!_httpClient.MatchedClientDeviceApp())
                {
                   // purgeOlderThan = DateTime.UtcNow;
                    StackifyAPILogger.Log("Upload metrics skipped due to being disabled");
                }
                else if (!_httpClient.IsAuthorized())
                {
                    // purgeOlderThan = DateTime.UtcNow;
                    StackifyAPILogger.Log("Upload metrics skipped authorization failure");
                }
                else if (!_httpClient.IsRecentError())
                {

                    //If something happens at 2:39:45. The OccurredUtc is a rounded down value to 2:39. So we add a minute to ensure the minute has fully elapsed
                    //We are doing 65 seconds to just a little lag time for queue processing
                    //doing metric counters only every 30 seconds.

                    metrics =
                        _AggregateMetrics.Where(
                            x => x.Value.OccurredUtc < currentMinute).Take(50).ToList();


                    if (metrics.Count > 0)
                    {
                        

                        //only getting metrics less than 10 minutes old to drop old data in case we get backed up
                        //they are removed from the _AggregateMetrics in the upload function upon success
                        success =
                            UploadAggregates(
                                metrics.Where(x => x.Value.OccurredUtc > DateTime.UtcNow.AddMinutes(-10)).ToList());

                    
                    }
                }
                else
                {
                    StackifyAPILogger.Log("Upload metrics skipped and delayed due to recent error"); 
                }

            }
            catch (Exception ex)
            {
                success = false;
                StackifyAPILogger.Log("Error uploading metrics " + ex.ToString());

                //if an error put them back in
                try
                {
                    metrics.ForEach(x => _AggregateMetrics.TryAdd(x.Key, x.Value));

                }
                catch (Exception ex2)
                {
                    StackifyAPILogger.Log("Error adding metrics back to upload list " + ex.ToString());
                }
            }

            return success;

        }

        private static void PurgeOldMetrics(DateTime purgeOlderThan)
        {
            try
            {
                //beginning of this method we save off latest aggregates before upload or purge

                //purge old stuff
                //if uploading disabled it purges everything
                //if success uploading then should be nothing to purge as that is already done above

                var oldMetrics = _AggregateMetrics.Where(x => x.Value.OccurredUtc < purgeOlderThan).ToList();
                MetricAggregate oldval;
                oldMetrics.ForEach(x => _AggregateMetrics.TryRemove(x.Key, out oldval));

            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Error purging metrics " + ex.ToString());
            }

        }

        private static void SetLatestAggregates(List<MetricAggregate> aggregates)
        {
            foreach (var item in aggregates)
            {
                MetricAggregate current;

                if (_LastAggregates.TryGetValue(item.NameKey, out current))
                {
                    //if newer update it
                    if (item.OccurredUtc > current.OccurredUtc)
                    {
                        _LastAggregates[item.NameKey] = item;
                    }
                }
                else
                {
                    //does not exist

                    _LastAggregates[item.NameKey] = item;
                }
            }
        }
        

        private static bool UploadAggregates(List<KeyValuePair<string,MetricAggregate>> metrics)
        {
            bool allSuccess = true;


            bool identifyResult = _httpClient.IdentifyApp();

            if (identifyResult && _httpClient.MatchedClientDeviceApp() && !_httpClient.IsRecentError() && _httpClient.IsAuthorized())
            {
                StackifyAPILogger.Log("Uploading Aggregate Metrics: " + metrics.Count);

                foreach (var keyValuePair in metrics)
                {
                    GetMetricResponse monitorInfo;

                    //in case the appid changes on the server side somehow and we need to update the monitorids we are adding the appid to the key
                    //calling IdentifyApp() above will sometimes cause the library to sync with the server with the appid
                    string keyWithAppID = string.Format("{0}-{1}", keyValuePair.Value.NameKey, _httpClient.AppIdentity.DeviceAppID);
                    
                    if (!_MontorIDList.ContainsKey(keyWithAppID))
                    {
                        monitorInfo = GetMonitorInfo(keyValuePair.Value);
                        if (monitorInfo != null && monitorInfo.MonitorID != null && monitorInfo.MonitorID > 0)
                        {
                            _MontorIDList[keyWithAppID] = monitorInfo;
                        }
                        else if (monitorInfo != null && monitorInfo.MonitorID == null)
                        {
                            StackifyAPILogger.Log("Unable to get metric info for " + keyWithAppID + " MonitorID is null");
                            _MontorIDList[keyWithAppID] = monitorInfo;
                        }
                        else
                        {
                            StackifyAPILogger.Log("Unable to get metric info for " + keyWithAppID);
                            allSuccess = false;
                        }
                    }
                    else
                    {
                        monitorInfo = _MontorIDList[keyWithAppID];
                    }

                    if (monitorInfo == null || monitorInfo.MonitorID == null)
                    {
                        StackifyAPILogger.Log("Metric info missing for " + keyWithAppID);
                        keyValuePair.Value.MonitorID = null;
                        allSuccess = false;
                    }
                    else
                    {
                        keyValuePair.Value.MonitorID = monitorInfo.MonitorID;
                    }
                }

                //get the identified ones
                var toUpload = metrics.Where(x => x.Value.MonitorID != null).ToList();

                bool success = UploadMetrics(toUpload.Select(x => x.Value).ToList());

                if (!success)
                {
                    //error uploading so add them back in
                    toUpload.ForEach(x => _AggregateMetrics.TryAdd(x.Key, x.Value));
                    allSuccess = false;
                }
                else
                {
                    //worked so remove them
                   MetricAggregate removed;
                   toUpload.ForEach(x=> _AggregateMetrics.TryRemove(x.Key, out removed)); 
                }
            }
            else 
            {
                StackifyAPILogger.Log("Metrics not uploaded. Identify Result: " + identifyResult + ", Metrics API Enabled: " + _httpClient.MatchedClientDeviceApp());

                //if there was an issue trying to identify the app we could end up here and will want to try again later
                allSuccess = false;
                //add them back to the queue
                metrics.ForEach(x=> _AggregateMetrics.TryAdd(x.Key, x.Value));
            }

            return allSuccess;
        }

        private static bool UploadMetrics(List<MetricAggregate> metrics)
        {
            try
            {
                if (metrics == null || metrics.Count == 0)
                    return true;

                //checks are done outside this method before it gets this far to ensure API access is working

                List<SubmitMetricByIDModel> records = new List<SubmitMetricByIDModel>();

                foreach (var metric in metrics)
                {
                    SubmitMetricByIDModel model = new SubmitMetricByIDModel();
                    model.Value = Math.Round(metric.Value, 2);
                    model.MonitorID = metric.MonitorID ?? 0;
                    model.OccurredUtc = metric.OccurredUtc;
                    model.Count = metric.Count;
                    model.MonitorTypeID = (short)metric.MetricType;

                    if (_httpClient.AppIdentity != null)
                    {
                        model.ClientDeviceID = _httpClient.AppIdentity.DeviceID;
                    }

                    records.Add(model);

                    StackifyAPILogger.Log(string.Format("Uploading metric {0}:{1} Count {2}, Value {3}, ID {4}", metric.Category, metric.Name, metric.Count, metric.Value, metric.MonitorID));
                }



                string jsonData = JsonConvert.SerializeObject(records);

                var response = _httpClient.SendJsonAndGetResponse(
                        (_httpClient.BaseAPIUrl) +
                        "Metrics/SubmitMetricsByID",
                        jsonData);


                if (response.Exception == null && response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                
                if (response.Exception != null)
                {
                    StackifyAPILogger.Log("Error saving metrics " + response.Exception.Message);    
                }

                return false;
            }
            catch (Exception e)
            {
                StackifyAPILogger.Log("Error saving metrics " + e.Message);
                return false;
            }
        }

        private static GetMetricResponse GetMonitorInfo(MetricAggregate metric)
        {
            try
            {
                if (_httpClient.IsRecentError() || !_httpClient.MatchedClientDeviceApp())
                {
                    return null;
                }

                GetMetricRequest request = new GetMetricRequest();

                if (_httpClient.AppIdentity != null)
                {
                    request.DeviceAppID = _httpClient.AppIdentity.DeviceAppID;
                    request.DeviceID = _httpClient.AppIdentity.DeviceID;
                    request.AppNameID = _httpClient.AppIdentity.AppNameID;
                }
                request.MetricName = metric.Name;
                request.MetricTypeID = (short)metric.MetricType;
                request.Category = metric.Category;

                string jsonData = JsonConvert.SerializeObject(request);


                var response =
                    _httpClient.SendJsonAndGetResponse(
                        (_httpClient.BaseAPIUrl) +
                        "Metrics/GetMetricInfo",
                        jsonData);


                if (response.Exception == null && response.StatusCode == HttpStatusCode.OK)
                {
                    var metricResponse = JsonConvert.DeserializeObject<GetMetricResponse>(response.ResponseText);

                    return metricResponse;
                }
               
                return null;
            }
            catch (Exception e)
            {
                StackifyAPILogger.Log("Error getting monitor info " + e.Message);
                return null;
            }
        }
    }
}
