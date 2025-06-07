using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Utility.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BeatSaberExtensions.Utility.Http;

public abstract class BaseHttpClient(
    Uri baseUri,
    bool logWhenSuccessful = false,
    TimeSpan? timeout = null,
    JsonSerializerSettings settings = null
) : IDisposable
{
    private static readonly JsonSerializerSettings _defaultJsonSerializerSettings =
        new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

    private readonly HttpClient _client = new HttpClient
    {
        Timeout = timeout ?? TimeSpan.FromSeconds(15),
    };
    private readonly JsonSerializerSettings _jsonSerializerSettings =
        settings ?? _defaultJsonSerializerSettings;

    public void Dispose() => _client?.Dispose();

    protected T SendHttpRequest<T>(
        HttpMethod method = null,
        string relativePath = "/",
        NameValueCollection queryParams = null,
        T defaultValue = default
    ) =>
        SendHttpRequestAsync(method, relativePath, queryParams, defaultValue)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    private async Task<T> SendHttpRequestAsync<T>(
        HttpMethod method = null,
        string relativePath = "/",
        NameValueCollection queryParams = null,
        T defaultValue = default
    )
    {
        var requestMessage = BuildHttpRequest(baseUri, method, relativePath, queryParams);

        HttpResponseMessage responseMessage;
        string responseContent;

        try
        {
            responseMessage = await _client.SendAsync(requestMessage).ConfigureAwait(false);
            responseContent = await responseMessage
                .Content.ReadAsStringAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogObject(
                new
                {
                    RequestUri = requestMessage.RequestUri.AbsoluteUri,
                    ExceptionType = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message,
                },
                "Encountered an exception calling endpoint",
                LogAction.Error
            );

            return defaultValue;
        }

        if (responseMessage is { IsSuccessStatusCode: false, StatusCode: var statusCode })
        {
            Logger.LogObject(
                new
                {
                    RequestUri = requestMessage.RequestUri.AbsoluteUri,
                    StatusCode = $"{(int)statusCode} {statusCode}",
                    ResponseBody = responseContent,
                },
                "Status code does not indicate success",
                LogAction.Error
            );

            return defaultValue;
        }

        if (!TryDeserialize(responseContent, out T result))
        {
            Logger.LogObject(
                new
                {
                    RequestUri = requestMessage.RequestUri.AbsoluteUri,
                    ResponseBody = responseContent,
                },
                "Failed to deserialize result",
                LogAction.Error
            );

            return defaultValue;
        }

        if (logWhenSuccessful)
        {
            Logger.LogObject(
                new
                {
                    RequestUri = requestMessage.RequestUri.AbsoluteUri,
                    ResponseContent = responseContent,
                },
                "Response from API",
                truncateAfterChars: int.MaxValue
            );
        }

        return result;
    }

    private bool TryDeserialize<T>(string value, out T result)
    {
        try
        {
            result =
                typeof(T) == typeof(string)
                    ? (T)(object)value
                    : JsonConvert.DeserializeObject<T>(value, _jsonSerializerSettings);

            return true;
        }
        catch
        {
            result = default;

            return false;
        }
    }

    private static HttpRequestMessage BuildHttpRequest(
        Uri baseUri,
        HttpMethod method = null,
        string relativePath = "/",
        NameValueCollection queryParams = null
    ) =>
        new HttpRequestMessage
        {
            Method = method ?? HttpMethod.Get,
            RequestUri = new UriBuilder
            {
                Scheme = baseUri.Scheme,
                Host = baseUri.Host,
                Port = baseUri.Port,
                Path = $"{baseUri.AbsolutePath.TrimEnd('/')}/{relativePath.Trim('/')}",
                Query = BuildQueryString(baseUri, queryParams),
            }.Uri,
        };

    private static string BuildQueryString(Uri baseUri, NameValueCollection queryParams)
    {
        var query = HttpUtility.ParseQueryString(baseUri.Query);

        foreach (var key in queryParams?.AllKeys ?? [])
            query[key] = queryParams[key];

        return query.ToString();
    }
}
