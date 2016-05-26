using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StackifyLib.Models
{
    public class Device
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Alias { get; set; }
        public string Env { get; set; }
        public int? EnvId { get; set; }
        public string Loc { get; set; }
        public int? LocId { get; set; }
        public string Alerts { get; set; }
    }

    public class Monitor
    {
        public int Id { get; set; }
        public short? MonitorTypeId { get; set; }
        public bool? IsHeading { get; set; }
        public string Desc { get; set; }
        public int? SortOrder { get; set; }
        public DateTime? LastCheck { get; set; }
        public string Status { get; set; }
        public string SparkLine { get; set; }
        public int? CurrentSevId { get; set; }
        public int? AlertingSevId { get; set; }
        public int? MonitorRelationId { get; set; }
    }

    public class MonitorMetric
    {
        public List<Metric> Metrics { get; set; }
        public float? Min { get; set; }
        public float? Max { get; set; }
        public float? Avg { get; set; }
        public string MonitorDesc { get; set; }
        public string Units { get; set; }
        public int? MonitorTypeId { get; set; }

        public class Metric
        {
            public DateTime Dt { get; set; }
            public double? Val { get; set; }
        }
    }
}
