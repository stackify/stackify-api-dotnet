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
        private static HttpClient _httpClient = null;
        private static HttpClient HttpClient
        {
            get
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient(null, null);
                }

                return _httpClient;
            }
        }

        private static readonly ConcurrentQueue<Metric> MetricQueue;

        private static readonly ConcurrentDictionary<string, GetMetricResponse> MontorIdList;
        private static readonly ConcurrentDictionary<string, MetricAggregate> AggregateMetrics;
        private static readonly ConcurrentDictionary<string, MetricAggregate> LastAggregates;
        private static readonly ConcurrentDictionary<string, MetricSetting> MetricSettings;

        private static readonly Timer Timer;

        private static bool _stopRequested;
        private static bool _metricsEverUsed;


        static MetricClient()
        {
            MetricQueue = new ConcurrentQueue<Metric>();

            MontorIdList = new ConcurrentDictionary<string, GetMetricResponse>();
            AggregateMetrics = new ConcurrentDictionary<string, MetricAggregate>();
            LastAggregates = new ConcurrentDictionary<string, MetricAggregate>();
            MetricSettings = new ConcurrentDictionary<string, MetricSetting>();

            Timer = new Timer(UploadMetricsCheck, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }


        public static int QueueSize => MetricQueue?.Count ?? 0;

        /// <summary>
        /// Used to make sure we report 0 values if nothing new comes in
        /// </summary>
        public static void HandleZeroReports(DateTime currentMinute)
        {
            foreach (KeyValuePair<string, MetricAggregate> item in LastAggregates)
            {
                MetricSetting setting;

                if (MetricSettings.TryGetValue(item.Value.NameKey, out setting))
                {
                    if (setting == null)
                    {
                        MetricSetting remove;
                        MetricSettings.TryRemove(item.Value.NameKey, out remove);
                        continue;
                    }

                    var agg = new MetricAggregate(item.Value.Category, item.Value.Name, item.Value.MetricType, item.Value.IsIncrement);
                    agg.OccurredUtc = currentMinute;

                    switch (item.Value.MetricType)
                    {
                        case MetricType.Counter:
                            setting.AutoReportLastValueIfNothingReported = false; // do not allow this
                            break;
                        case MetricType.CounterTime:
                            setting.AutoReportLastValueIfNothingReported = false; // do not allow this
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

                    var aggKey = agg.AggregateKey();

                    if (AggregateMetrics.ContainsKey(aggKey) == false)
                    {
                        agg.NameKey = item.Value.NameKey;

                        StackifyAPILogger.Log($"Creating default value for {aggKey}");

                        AggregateMetrics[aggKey] = agg;
                    }
                }
            }
        }

        public static List<LatestAggregate> GetLatestMetrics()
        {
            var latest = new List<LatestAggregate>();

            foreach (KeyValuePair<string, MetricAggregate> item in LastAggregates)
            {
                var findVal = item.Value;

                var agg = new LatestAggregate
                {
                    Category = findVal.Category,
                    Count = findVal.Count,
                    MetricType = findVal.MetricType,
                    MetricID = findVal.MonitorID,
                    Name = findVal.Name,
                    OccurredUtc = findVal.OccurredUtc,
                    Value = findVal.Value
                };

                latest.Add(agg);
            }

            return latest;
        }

        public static LatestAggregate GetLatestMetric(string category, string metricName)
        {
            IEnumerable<KeyValuePair<string, MetricAggregate>> list = LastAggregates
                .Where(x => x.Value.Category.Equals(category, StringComparison.OrdinalIgnoreCase) && x.Value.Name.Equals(metricName, StringComparison.OrdinalIgnoreCase));

            foreach (KeyValuePair<string, MetricAggregate> item in list)
            {
                var findVal = item.Value;

                var agg = new LatestAggregate
                {
                    Category = findVal.Category,
                    Count = findVal.Count,
                    MetricType = findVal.MetricType,
                    MetricID = findVal.MonitorID,
                    Name = findVal.Name,
                    OccurredUtc = findVal.OccurredUtc,
                    Value = findVal.Value
                };

                return agg;
            }

            return null;
        }

        public static void QueueMetric(Metric metric)
        {
            _metricsEverUsed = true;

            try
            {
                //set a sanity cap
                if (MetricQueue.Count < 100000)
                {
                    MetricQueue.Enqueue(metric);

                    // RT-561: Incremented Metrics Broken
                    if (metric.IsIncrement)
                    {
                        var nameKey = metric.CalcNameKey();

                        if (LastAggregates.ContainsKey(metric.CalcNameKey()))
                        {
                            LastAggregates[nameKey].OccurredUtc = metric.Occurred;
                            LastAggregates[nameKey].Value += metric.Value;
                        }
                        else
                        {
                            var agg = new MetricAggregate(metric);
                            agg.OccurredUtc = metric.Occurred;
                            agg.Value = metric.Value;

                            LastAggregates.TryAdd(nameKey, agg);
                        }
                    }
                }
                else
                {
                    StackifyAPILogger.Log("No longer queuing new metrics because more than 100000 are queued", true);
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log(ex.ToString());
            }
        }

        private static void Aggregate(MetricAggregate aggregate)
        {
            try
            {
                var aggKey = aggregate.AggregateKey();

                MetricAggregate agg;
                if (AggregateMetrics.TryGetValue(aggKey, out agg) == false)
                {
                    if (AggregateMetrics.Count > 1000)
                    {
                        StackifyAPILogger.Log("No longer aggregating new metrics because more than 1000 are queued", true);
                        return;
                    }

                    StackifyAPILogger.Log($"Creating aggregate for {aggKey}");

                    AggregateMetrics[aggKey] = aggregate;
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

                AggregateMetrics[aggKey] = agg;
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log($"Error in StackifyLib with aggregating metrics\r\n{ex}");
            }
        }

        /// <summary>
        /// Read everything in the queue up to a certain time point
        /// </summary>
        private static void ReadAllQueuedMetrics()
        {
            // read only up until now so it doesn't get stuck in an endless loop
            // Loop through add sum up the totals of the counts and values by aggregate key then pass it all in at once to update the aggregate dictionary so it is done in one pass
            // key is the aggregate key which is the metric name, type and rounded minute of the occurrence

            var maxDate = DateTime.UtcNow;

            StackifyAPILogger.Log($"ReadAllQueuedMetrics {maxDate}");

            var batches = new Dictionary<string, MetricAggregate>();

            long processed = 0;

            Metric metric;
            while (MetricQueue.TryDequeue(out metric))
            {
                processed++;

                metric.CalcAndSetAggregateKey();

                if (batches.ContainsKey(metric.AggregateKey) == false)
                {
                    var nameKey = metric.CalcNameKey();

                    var agg = new MetricAggregate(metric);

                    if (metric.IsIncrement)
                    {
                        if (LastAggregates.ContainsKey(nameKey))
                        {
                            // if wanting to do increments we need to grab the last value so we know what to increment
                            metric.Value = LastAggregates[nameKey].Value;
                        }
                    }

                    batches[metric.AggregateKey] = agg;

                    // if it is null don't do anything
                    // we are doing it where the aggregates are created so we don't do it one very single metric, just once per batch to optimize performance
                    if (metric.Settings != null)
                    {
                        MetricSettings[nameKey] = metric.Settings;
                    }
                }

                batches[metric.AggregateKey].Count++;

                if (metric.IsIncrement)
                {
                    // safety
                    var val = batches[metric.AggregateKey].Value;
                    if (val.Equals(double.MinValue) || val.Equals(double.MaxValue))
                    {
                        batches[metric.AggregateKey].Value = 0;

                        StackifyAPILogger.Log($"Read queued metrics reset increment {metric.AggregateKey} to zero due to min/max value of {val}");
                    }

                    //add or subtract
                    batches[metric.AggregateKey].Value += metric.Value;

                    // allow negative?
                    if (metric.Settings != null && batches[metric.AggregateKey].Value < 0 && metric.Settings.AllowNegativeGauge == false)
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

            StackifyAPILogger.Log($"Read queued metrics processed {processed} for max date {maxDate}");

            foreach (KeyValuePair<string, MetricAggregate> batch in batches)
            {
                Aggregate(batch.Value);
            }
        }

        private static void UploadMetricsCheck(object state)
        {
            StackifyAPILogger.Log("Upload metrics check");

            Timer.Change(-1, -1);

            double seconds = 2; //read quickly in case there is a very high volume to keep queue size down

            if (_stopRequested == false)
            {
                var allSuccess = false;
                var purgeOlderThan = DateTime.UtcNow.AddMinutes(-10);

                var currentMinute = DateTime.UtcNow.Floor(TimeSpan.FromMinutes(1));

                StackifyAPILogger.Log($"Calling UploadMetrics {currentMinute}");

                allSuccess = UploadMetrics(currentMinute);

                PurgeOldMetrics(purgeOlderThan);

                if (AggregateMetrics.Count > 0 && allSuccess)
                {
                    seconds = .1;
                }
            }
            else
            {
                StackifyAPILogger.Log("Metrics processing canceled because stop was requested");
            }

            Timer.Change(TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(seconds));
        }

        public static void StopMetricsQueue(string reason = "Unknown")
        {
            if (_metricsEverUsed == false)
            {
                return;
            }

            try
            {
                StackifyAPILogger.Log($"StopMetricsQueue called by {reason}", true);

                //don't let t his method run more than once
                if (_stopRequested)
                {
                    return;
                }

                _stopRequested = true;

                var currentMinute = DateTime.UtcNow.AddMinutes(2).Floor(TimeSpan.FromMinutes(1));

                UploadMetrics(currentMinute);

                _stopRequested = false;

                StackifyAPILogger.Log($"StopMetricsQueue completed {reason}", true);
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log($"StopMetricsQueue error {ex}", true);
            }
        }

        public static bool UploadMetrics(DateTime currentMinute)
        {
            var success = false;
            var metrics = new List<KeyValuePair<string, MetricAggregate>>();

            try
            {
                //read everything up to now
                ReadAllQueuedMetrics();

                //ensures all the aggregate keys exists for any previous metrics so we report zeros on no changes
                HandleZeroReports(currentMinute);

                List<MetricAggregate> getForRecent = AggregateMetrics
                    .Where(x => x.Value.OccurredUtc < currentMinute && x.Value.OccurredUtc > DateTime.UtcNow.AddMinutes(-5))
                    .Select(x => x.Value)
                    .ToList();

                SetLatestAggregates(getForRecent);

                //skip messing with HttpClient if nothing to do
                if (AggregateMetrics.Count == 0)
                {
                    return true;
                }

                if (HttpClient.MatchedClientDeviceApp() == false)
                {
                    // purgeOlderThan = DateTime.UtcNow;
                    StackifyAPILogger.Log("Upload metrics skipped because we were unable to match the app to an app in Stackify");
                }
                else if (HttpClient.IsAuthorized() == false)
                {
                    // purgeOlderThan = DateTime.UtcNow;
                    StackifyAPILogger.Log("Upload metrics skipped authorization failure");
                }
                else if (HttpClient.IsRecentError() == false)
                {
                    //If something happens at 2:39:45. The OccurredUtc is a rounded down value to 2:39. So we add a minute to ensure the minute has fully elapsed
                    //We are doing 65 seconds to just a little lag time for queue processing
                    //doing metric counters only every 30 seconds.

                    metrics = AggregateMetrics
                        .Where(x => x.Value.OccurredUtc < currentMinute)
                        .Take(50)
                        .ToList();

                    if (metrics.Count > 0)
                    {
                        //only getting metrics less than 10 minutes old to drop old data in case we get backed up
                        //they are removed from the _AggregateMetrics in the upload function upon success
                        success = UploadAggregates(metrics
                            .Where(x => x.Value.OccurredUtc > DateTime.UtcNow.AddMinutes(-10))
                            .ToList());
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
                StackifyAPILogger.Log($"Error uploading metrics {ex}");

                //if an error put them back in
                try
                {
                    metrics.ForEach(x => AggregateMetrics.TryAdd(x.Key, x.Value));
                }
                catch (Exception ex2)
                {
                    StackifyAPILogger.Log($"Error adding metrics back to upload list {ex2}");
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

                List<KeyValuePair<string, MetricAggregate>> oldMetrics = AggregateMetrics
                    .Where(x => x.Value.OccurredUtc < purgeOlderThan)
                    .ToList();

                oldMetrics.ForEach(x =>
                {
                    MetricAggregate oldval;
                    AggregateMetrics.TryRemove(x.Key, out oldval);
                });
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log($"Error purging metrics {ex}");
            }

        }

        private static void SetLatestAggregates(List<MetricAggregate> aggregates)
        {
            foreach (var item in aggregates)
            {
                MetricAggregate current;

                if (LastAggregates.TryGetValue(item.NameKey, out current))
                {
                    //if newer update it
                    if (item.OccurredUtc > current.OccurredUtc)
                    {
                        LastAggregates[item.NameKey] = item;
                    }
                }
                else
                {
                    //does not exist

                    LastAggregates[item.NameKey] = item;
                }
            }
        }

        private static bool UploadAggregates(List<KeyValuePair<string, MetricAggregate>> metrics)
        {
            var allSuccess = true;

            if (metrics.Count == 0)
            {
                return true;
            }

            var identifyResult = HttpClient.IdentifyApp();

            if (identifyResult && HttpClient.MatchedClientDeviceApp() && HttpClient.IsRecentError() == false && HttpClient.IsAuthorized())
            {
                StackifyAPILogger.Log($"Uploading Aggregate Metrics: {metrics.Count}");

                foreach (KeyValuePair<string, MetricAggregate> keyValuePair in metrics)
                {
                    GetMetricResponse monitorInfo;

                    // in case the appid changes on the server side somehow and we need to update the monitorids we are adding the appid to the key
                    // calling IdentifyApp() above will sometimes cause the library to sync with the server with the appid
                    var keyWithAppId = string.Format("{0}-{1}", keyValuePair.Value.NameKey, HttpClient.AppIdentity.DeviceAppID);

                    if (MontorIdList.ContainsKey(keyWithAppId) == false)
                    {
                        monitorInfo = GetMonitorInfo(keyValuePair.Value);
                        if (monitorInfo != null && monitorInfo.MonitorID != null && monitorInfo.MonitorID > 0)
                        {
                            MontorIdList[keyWithAppId] = monitorInfo;
                        }
                        else if (monitorInfo != null && monitorInfo.MonitorID == null)
                        {
                            StackifyAPILogger.Log($"Unable to get metric info for {keyWithAppId} MonitorID is null");
                            MontorIdList[keyWithAppId] = monitorInfo;
                        }
                        else
                        {
                            StackifyAPILogger.Log($"Unable to get metric info for {keyWithAppId}");
                            allSuccess = false;
                        }
                    }
                    else
                    {
                        monitorInfo = MontorIdList[keyWithAppId];
                    }

                    if (monitorInfo == null || monitorInfo.MonitorID == null)
                    {
                        StackifyAPILogger.Log($"Metric info missing for {keyWithAppId}");
                        keyValuePair.Value.MonitorID = null;
                        allSuccess = false;
                    }
                    else
                    {
                        keyValuePair.Value.MonitorID = monitorInfo.MonitorID;
                    }
                }

                //get the identified ones
                List<KeyValuePair<string, MetricAggregate>> toUpload = metrics.Where(x => x.Value.MonitorID != null).ToList();

                var success = UploadMetrics(toUpload.Select(x => x.Value).ToList());

                if (success == false)
                {
                    //error uploading so add them back in
                    toUpload.ForEach(x => AggregateMetrics.TryAdd(x.Key, x.Value));
                    allSuccess = false;
                }
                else
                {
                    //worked so remove them
                    toUpload.ForEach(x =>
                    {
                        MetricAggregate removed;
                        AggregateMetrics.TryRemove(x.Key, out removed);
                    });
                }
            }
            else
            {
                StackifyAPILogger.Log($"Metrics not uploaded. Identify Result: {identifyResult}, Metrics API Enabled: {HttpClient.MatchedClientDeviceApp()}");

                //if there was an issue trying to identify the app we could end up here and will want to try again later
                allSuccess = false;

                //add them back to the queue
                metrics.ForEach(x => AggregateMetrics.TryAdd(x.Key, x.Value));
            }

            return allSuccess;
        }

        private static bool UploadMetrics(List<MetricAggregate> metrics)
        {
            try
            {
                if (metrics == null || metrics.Count == 0)
                {
                    return true;
                }

                //checks are done outside this method before it gets this far to ensure API access is working

                var records = new List<SubmitMetricByIDModel>();

                foreach (var metric in metrics)
                {
                    var model = new SubmitMetricByIDModel
                    {
                        Value = Math.Round(metric.Value, 2),
                        MonitorID = metric.MonitorID ?? 0,
                        OccurredUtc = metric.OccurredUtc,
                        Count = metric.Count,
                        MonitorTypeID = (short)metric.MetricType
                    };

                    if (HttpClient.AppIdentity != null)
                    {
                        model.ClientDeviceID = HttpClient.AppIdentity.DeviceID;
                    }

                    records.Add(model);

                    StackifyAPILogger.Log(string.Format($"Uploading metric {metric.Category}:{metric.Name} Count {metric.Count}, Value {metric.Value}, ID {metric.MonitorID}"));
                }

                var jsonData = JsonConvert.SerializeObject(records);

                var response = HttpClient.SendJsonAndGetResponse($"{HttpClient.BaseAPIUrl}Metrics/SubmitMetricsByID", jsonData);

                if (response.Exception == null && response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }

                if (response.Exception != null)
                {
                    StackifyAPILogger.Log($"Error saving metrics {response.Exception.Message}");
                }

                return false;
            }
            catch (Exception e)
            {
                StackifyAPILogger.Log($"Error saving metrics {e}");
                return false;
            }
        }

        private static GetMetricResponse GetMonitorInfo(MetricAggregate metric)
        {
            try
            {
                if (HttpClient.IsRecentError() || HttpClient.MatchedClientDeviceApp() == false)
                {
                    return null;
                }

                var request = new GetMetricRequest();

                if (HttpClient.AppIdentity != null)
                {
                    request.DeviceAppID = HttpClient.AppIdentity.DeviceAppID;
                    request.DeviceID = HttpClient.AppIdentity.DeviceID;
                    request.AppNameID = HttpClient.AppIdentity.AppNameID;
                }

                request.MetricName = metric.Name;
                request.MetricTypeID = (short)metric.MetricType;
                request.Category = metric.Category;

                var jsonData = JsonConvert.SerializeObject(request);

                var response = HttpClient.SendJsonAndGetResponse($"{HttpClient.BaseAPIUrl}Metrics/GetMetricInfo", jsonData);

                if (response.Exception == null && response.StatusCode == HttpStatusCode.OK)
                {
                    var metricResponse = JsonConvert.DeserializeObject<GetMetricResponse>(response.ResponseText);

                    return metricResponse;
                }

                return null;
            }
            catch (Exception e)
            {
                StackifyAPILogger.Log($"Error getting monitor info {e}");
                return null;
            }
        }
    }
}