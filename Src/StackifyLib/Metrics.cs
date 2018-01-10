using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StackifyLib.Internal.Metrics;
using StackifyLib.Models;

namespace StackifyLib
{
    /*
        Different types of metrics
        Count or Sum of values over a minte

        Guage - incremented or decremented - reports the last value
        Increment guage function    

        Average - Reports an average of a sample of values
        Rolling average - Could report an average over a longer time span than 1 minute  #FUTURE?

        Time - Average time someting took in 1 minute
        Rolling average time - Rolling average over longer period of time #FUTURE?

  
        ### Percentile not currently supported ###
        Percentiles in time span - calculated over last X metrics received in a minute
        Rolling percentiles - calculated over last X metrics over a longer timepsan    
    */

    /// <summary>
    /// Used to log custom metrics
    /// </summary>
    public class Metrics
    {
        public static LatestAggregate GetLatest(string category, string metricName)
        {
            return MetricClient.GetLatestMetric(category, metricName);
        }

        public static List<LatestAggregate> GetLatestAllMetrics()
        {
            return MetricClient.GetLatestMetrics();
        }

        /// <summary>
        /// Guage type metric that reports the last value reported once a minute
        /// </summary>
        /// <param name="category">Category of the metric</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="value">Explicit value to set the metric to</param>
        /// <param name="autoResendLastValueIfNothingReported">Every minute resend the last value if nothing reported</param>
        public static void SetGauge(string category, string metricName, double value, bool autoResendLastValueIfNothingReported = false)
        {
            var m = new Metric(category, metricName, MetricType.MetricLast) { Value = value, Settings = new MetricSetting { AutoReportLastValueIfNothingReported = autoResendLastValueIfNothingReported } };
            
            MetricClient.QueueMetric(m);
        }

        /// <summary>
        /// Increment or decrement a guage metric type
        /// </summary>
        /// <param name="category">Category of the metric</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="incrementBy">Value can be positive or negative to decrement. Defaults to 1</param>
        public static void IncrementGauge(string category, string metricName, double incrementBy, MetricSetting advancedSettings)
        {
            //leaving the count as 1 below because when it gets processed later it would sum up the count there
            var m = new Metric(category, metricName, MetricType.MetricLast);
            m.Value = incrementBy;
            m.IsIncrement = true;
            m.Settings = advancedSettings;

            MetricClient.QueueMetric(m);
        }

        /// <summary>
        /// Increment or decrement a guage metric type
        /// </summary>
        /// <param name="category">Category of the metric</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="incrementBy">Value can be positive or negative to decrement. Defaults to 1</param>
        /// <param name="autoResendLastValueIfNothingReported">Every minute resend the last value if nothing reported</param>
        public static void IncrementGauge(string category, string metricName, double incrementBy = 1, bool autoResendLastValueIfNothingReported = false)
        {
            IncrementGauge(category, metricName, incrementBy, new MetricSetting {AutoReportLastValueIfNothingReported = autoResendLastValueIfNothingReported});
        }

        /// <summary>
        /// Sums up the values passed in and reports the average of the values once per minute
        /// </summary>
        /// <param name="category">Category of the metric</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="value"></param>
        public static void Average(string category, string metricName, double value, MetricSetting advancedSettings = null)
        {
            var m = new Metric(category, metricName, MetricType.MetricAverage) { Value = value, Settings = advancedSettings };
            
            MetricClient.QueueMetric(m);
        }

        /// <summary>
        /// Sums up the values passed in and reports the total once per minute
        /// </summary>
        /// <param name="category">Category of the metric</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="value"></param>
        public static void Sum(string category, string metricName, double value, MetricSetting advancedSettings = null)
        {
            var m = new Metric(category, metricName, MetricType.Counter);
            m.Value = value;
            m.Settings = advancedSettings;
            
            MetricClient.QueueMetric(m);
        }

        /// <summary>
        /// Counts how many times something happens per minute
        /// </summary>
        /// <param name="category">Category of the metric</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="incrementBy"></param>
        /// <param name="autoSendIfZero">If nothing is reported for a minute, should we report a 0?</param>
        public static void Count(string category, string metricName, int incrementBy = 1, bool autoReportZeroIfNothingReported = false)
        {
            var m = new Metric(category, metricName, MetricType.Counter);
            m.Value = incrementBy;
            m.Settings = new MetricSetting { AutoReportZeroIfNothingReported = autoReportZeroIfNothingReported };

            MetricClient.QueueMetric(m);
        }

        /// <summary>
        /// Report when something started and this type of metric will calculate the average time it takes
        /// </summary>
        /// <param name="category">Category of the metric</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="startTime">When the event being tracked started</param>
        public static void Time(string category, string metricName, DateTimeOffset startTime)
        {
            TimeSpan timeTaken = DateTimeOffset.UtcNow.Subtract(startTime.ToUniversalTime());
            Time(category, metricName, timeTaken);
        }

        /// <summary>
        /// Report how long something took to happen and this type of metric will calculate the average time it takes
        /// </summary>
        /// <param name="category">Category of the metric</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="timeTaken">How long the event being tracked took</param>
        public static void Time(string category, string metricName, TimeSpan timeTaken)
        {
            MetricClient.QueueMetric(new Metric(category, metricName, MetricType.CounterTime) { Value = timeTaken.TotalSeconds});
        }

        /// <summary>
        /// Calculate average time taken and a second metric for how many times it occurred
        /// </summary>
        /// <param name="category">Category of the metric</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="startTime">When the event being tracked started</param>
        public static void CountAndTime(string category, string metricName, DateTimeOffset startTime, bool autoReportZeroIfNothingReported = false)
        {
            TimeSpan timeTaken = DateTimeOffset.UtcNow.Subtract(startTime.ToUniversalTime());
            CountAndTime(category, metricName, timeTaken, autoReportZeroIfNothingReported);
        }

        /// <summary>
        /// Calculate average time taken and a second metric for how many times it occurred
        /// </summary>
        /// <param name="category">Category of the metric</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="timeTaken">How long the event being tracked took</param>
        public static void CountAndTime(string category, string metricName, TimeSpan timeTaken, bool autoReportZeroIfNothingReported = false)
        {
            MetricClient.QueueMetric(new Metric(category, metricName, MetricType.Counter) { Value = 1, Settings = new MetricSetting() {AutoReportZeroIfNothingReported = autoReportZeroIfNothingReported} });
            MetricClient.QueueMetric(new Metric(category, metricName + " Time", MetricType.CounterTime) { Value = timeTaken.TotalSeconds });
        }
    }
}