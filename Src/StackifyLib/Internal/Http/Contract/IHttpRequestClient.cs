using System.Collections.Generic;
using System.Threading.Tasks;
using StackifyLib.Auth;

namespace StackifyLib.Http
{
    internal interface IHttpRequestClient
    {
        /// <summary>
        /// Post form URL encoded data
        /// </summary>
        /// <typeparam name="T">Deserialized Response</typeparam>
        /// <exception cref="UnauthorizedAccessException">Received 401 response</exception>
        /// <exception cref="HttpRequestException">Received failed HTTP response</exception>
        Task<T> PostAsync<T>(string uri, Dictionary<string, string> formData);

        /// <summary>
        /// Post JSON data
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">Received 401 response</exception>
        /// <exception cref="HttpRequestException">Received failed HTTP response</exception>
        Task PostAsync(string uri, object json, AccessTokenResponse token, bool compress = false);
    }
}
