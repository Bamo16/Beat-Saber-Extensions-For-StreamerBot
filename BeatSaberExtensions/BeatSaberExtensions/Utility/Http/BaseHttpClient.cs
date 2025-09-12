using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading.Tasks;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.UriExtensions;
using BeatSaberExtensions.Utility.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BeatSaberExtensions.Utility.Http;

#nullable enable

public abstract class BaseHttpClient(
    Uri baseUri,
    bool logWhenSuccessful = false,
    TimeSpan? timeout = null,
    JsonSerializerSettings? settings = null
) : IDisposable
{
    private readonly HttpClient _client = new HttpClient
    {
        Timeout = timeout ?? TimeSpan.FromSeconds(15),
    };
    private readonly JsonSerializerSettings _jsonSerializerSettings =
        settings
        ?? new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

    public void Dispose() => _client?.Dispose();

    protected T? SendHttpRequest<T>(
        HttpMethod? method = null,
        string relativePath = "/",
        NameValueCollection? queryParams = null,
        T? defaultValue = default
    ) =>
        SendAsync(method, relativePath, queryParams, defaultValue)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    private async Task<T?> SendAsync<T>(
        HttpMethod? method = null,
        string relativePath = "/",
        NameValueCollection? queryParams = null,
        T? defaultValue = default
    )
    {
        var requestMessage = baseUri.BuildHttpRequestMessage(method, relativePath, queryParams);

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

        if (!TryDeserialize(responseContent, out T? result))
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

    private bool TryDeserialize<T>(string value, out T? result)
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
}
