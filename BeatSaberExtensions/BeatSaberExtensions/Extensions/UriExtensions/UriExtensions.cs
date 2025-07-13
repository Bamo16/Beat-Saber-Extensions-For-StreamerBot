using System;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

namespace BeatSaberExtensions.Extensions.UriExtensions;

public static class UriExtensions
{
    public static NameValueCollection BuildQuery(
        this Uri baseUri,
        NameValueCollection queryParams
    ) =>
        (queryParams?.AllKeys ?? []).Aggregate(
            HttpUtility.ParseQueryString(baseUri.Query),
            (all, key) =>
            {
                all[key] = queryParams[key];
                return all;
            }
        );
}
