using System;

namespace StackifyLib.Utils
{
    internal class StackifyWebResponse
    {
        public string ResponseText { get; set; }
        public System.Net.HttpStatusCode StatusCode { get; set; }
        public Exception Exception { get; set; }
    }
}