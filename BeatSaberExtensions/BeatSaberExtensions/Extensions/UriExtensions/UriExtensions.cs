using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Web;

namespace BeatSaberExtensions.Extensions.UriExtensions;

#nullable enable

public static class UriExtensions
{
    public static HttpRequestMessage BuildHttpRequestMessage(
        this Uri baseUri,
        HttpMethod? method = null,
        string relativePath = "/",
        NameValueCollection? queryParams = null
    ) =>
        new HttpRequestMessage
        {
            Method = method ?? HttpMethod.Get,
            RequestUri = new UriBuilder
            {
                Scheme = baseUri.Scheme,
                Host = baseUri.Host,
                Port = baseUri.Port,
                Path = baseUri.BuildPath(relativePath),
                Query = baseUri.BuildQuery(queryParams).ToString(),
            }.Uri,
        };

    public static string BuildPath(this Uri baseUri, params string[] pathSegments) =>
        pathSegments.Aggregate(
            baseUri.AbsolutePath,
            (path, segment) => string.Join("/", path.TrimEnd('/'), segment.TrimStart('/'))
        );

    public static NameValueCollection BuildQuery(
        this Uri baseUri,
        NameValueCollection? queryParams
    ) =>
        (queryParams ??= []).AllKeys.Aggregate(
            HttpUtility.ParseQueryString(baseUri.Query),
            (all, key) =>
            {
                all[key] = queryParams[key];
                return all;
            }
        );
}
