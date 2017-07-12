using System;

namespace StackifyLib.Utils
{
    public class AppIdentityInfo
    {
        public int? DeviceID { get; set; }
        public int? DeviceAppID { get; set; }
        public Guid? AppNameID { get; set; }
        public short? EnvID { get; set; }
        public string AppName { get; set; }
        public string Env { get; set; }
        public Guid? AppEnvID { get; set; }
        public string DeviceAlias { get; set; }
    }
}
