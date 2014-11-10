using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using StackifyLib.Utils;
namespace StackifyLib.Models
{

    public enum MetricType : short
    {
        MetricLast = 134,
        Counter = 129,
        MetricAverage = 132,
        CounterTime = 131
    }

    public class MetricSetting
    {
        public bool AutoReportZeroIfNothingReported { get; set; }
        public bool AutoReportLastValueIfNothingReported { get; set; }
    }

    public class Metric
    {
        public Metric(string category, string name, MetricType metricType)
        {
            Category = category;
            MetricType = metricType;
            Name = name;
            Occurred = DateTime.UtcNow;
            IsIncrement = false;
            Settings = new MetricSetting() { AutoReportZeroIfNothingReported = false };
        }

        public MetricSetting Settings { get; set; }
        public bool IsIncrement { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public MetricType MetricType { get; set; }
        public double Value { get; set; }
        
        public DateTime Occurred { get; internal set; }

        public string AggregateKey { get; set; }

        public void CalcAndSetAggregateKey()
        {
            this.AggregateKey = Category.ToLower() + "-" + ((Name ?? "Missing Name")).ToLower() + "-" + MetricType.ToString() + "-" + GetRoundedTime().ToString("s");
        }

        public string CalcNameKey()
        {
            return Category.ToLower() + "-" + ((Name ?? "Missing Name")).ToLower() + "-" + MetricType.ToString();
        }

        public DateTime GetRoundedTime()
        {
            DateTime rounded;

            rounded = Occurred.Floor(TimeSpan.FromMinutes(1));

            return rounded;
        }
    }

    public class LatestAggregate
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public int? MetricID { get; set; }
        public DateTime OccurredUtc { get; set; }
        public double Value { get; set; }
        public int Count { get; set; }
        public MetricType MetricType { get; set; }
    }

    internal class MetricAggregate
    {
        public MetricAggregate(Metric metric)
        {
            Name = metric.Name;
            Category = metric.Category;
            MetricType = metric.MetricType;
            Value = 0;
            Count = 0;
            NameKey = metric.CalcNameKey();
            OccurredUtc = metric.GetRoundedTime();
        }

        public MetricAggregate(string category, string name, MetricType metricType)
        {
            Value = 0;
            Name = name;
            Category = category;
            MetricType = metricType;
        }
        public int Count { get; set; }
        public string Category { get; private set; }
        public string Name { get; private set; }
        public double Value { get; set; }
        
        public DateTime OccurredUtc { get; set; }
        public int? MonitorID { get; set; }
        public MetricType MetricType { get; private set; }

        public string NameKey { get; set; }


        public string AggregateKey()
        {
            return (Category ?? "Missing Category").ToLower() + "-" + ((Name ?? "Missing Name")).ToLower() + "-" + MetricType.ToString() + "-" + OccurredUtc.Floor(TimeSpan.FromMinutes(1)).ToString("s");
        }
    }

    internal class GetMetricRequest
    {
        public string Category { get; set; }
        public string MetricName { get; set; }
        public int? DeviceID { get; set; }
        public int? DeviceAppID { get; set; }
        public Guid? AppNameID { get; set; }

        public int MetricTypeID { get; set; } 
    }

    internal class GetMetricResponse
    {
        public int? MonitorID { get; set; }
    }

    public class SubmitMetricByIDModel
    {
        public int MonitorID { get; set; }
        public double Value { get; set; }
        public int Count { get; set; }
        public DateTime OccurredUtc { get; set; }
        public short? MonitorTypeID { get; set; }
    }

}
