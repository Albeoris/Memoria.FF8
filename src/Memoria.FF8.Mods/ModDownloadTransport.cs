using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Memoria.FF8.Mods;

internal static partial class ModDownloadTransport
{
    public static HttpClient CreateClient() => new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None, AllowAutoRedirect = false, UseCookies = true }) { Timeout = Timeout.InfiniteTimeSpan };

    public static (Uri Uri, bool Drive) DecodeLink(string link)
    {
        string value = link.StartsWith("iroj://", StringComparison.Ordinal) ? link[7..] : link;
        int separator = value.IndexOf('/');
        if (separator < 0)
        {
            throw new NotSupportedException("The download provider is unsupported.");
        }
        string kind = value[..separator];
        value = value[(separator + 1)..];
        int dollar = value.IndexOf('$');
        if (dollar >= 0)
        {
            value = value[..dollar] + "://" + value[(dollar + 1)..];
        }
        if (kind == "GDrive")
        {
            return (new Uri("https://drive.google.com/uc?export=download&id=" + Uri.EscapeDataString(value)), true);
        }
        if (kind == "Url" && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo))
        {
            return (uri, false);
        }
        throw new NotSupportedException("The download provider is unsupported.");
    }

    public static async Task<HttpResponseMessage> OpenAsync(HttpClient client, Uri uri, long? offset, string? validator, bool drive, CancellationToken token, string? ifNoneMatch = null, DateTimeOffset? ifModifiedSince = null)
    {
        for (int redirect = 0; redirect < 8; redirect++)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Memoria.FF8/1.0");
            request.Headers.AcceptEncoding.ParseAdd("identity");
            if (ifNoneMatch != null && EntityTagHeaderValue.TryParse(ifNoneMatch, out EntityTagHeaderValue? tag))
            {
                request.Headers.IfNoneMatch.Add(tag);
            }
            if (ifModifiedSince != null)
            {
                request.Headers.IfModifiedSince = ifModifiedSince;
            }
            if (offset > 0)
            {
                request.Headers.Range = new RangeHeaderValue(offset, null);
                request.Headers.TryAddWithoutValidation("If-Range", validator);
            }
            using CancellationTokenSource headersTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            headersTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headersTimeout.Token);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                Uri? location = response.Headers.Location;
                response.Dispose();
                if (location == null)
                {
                    throw new InvalidDataException("The provider returned an invalid redirect.");
                }
                Uri next = new(uri, location);
                if (next.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(next.UserInfo) || uri.Scheme == "https" && next.Scheme != "https")
                {
                    throw new InvalidDataException("The provider returned an unsafe redirect.");
                }
                uri = next;
                continue;
            }
            if (response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
            {
                using (response)
                {
                    if (!drive)
                    {
                        throw new InvalidDataException("The source returned a web page instead of the mod package.");
                    }
                    using Stream body = await response.Content.ReadAsStreamAsync(token);
                    using MemoryStream buffer = new();
                    byte[] chunk = new byte[16384];
                    int count;
                    while ((count = await body.ReadAsync(chunk, headersTimeout.Token)) > 0)
                    {
                        if (buffer.Length + count > 1024 * 1024)
                        {
                            throw new ModDownloadActionException();
                        }
                        buffer.Write(chunk, 0, count);
                    }
                    string html = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
                    Match form = FormPattern().Match(html);
                    if (!form.Success)
                    {
                        throw new ModDownloadActionException();
                    }
                    Dictionary<string, string> fields = new(StringComparer.Ordinal);
                    foreach (Match input in InputPattern().Matches(html))
                    {
                        Dictionary<string, string> attributes = Attributes(input.Value);
                        if (attributes.TryGetValue("name", out string? name))
                        {
                            fields[name] = attributes.GetValueOrDefault("value", "");
                        }
                    }
                    Dictionary<string, string> formAttributes = Attributes(form.Value);
                    if (!fields.ContainsKey("confirm") || !formAttributes.TryGetValue("action", out string? action))
                    {
                        throw new ModDownloadActionException();
                    }
                    Uri next = new(uri, WebUtility.HtmlDecode(action));
                    if (next.Scheme != "https" || !(next.Host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase) || next.Host.EndsWith(".googleusercontent.com", StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new ModDownloadActionException();
                    }
                    string query = string.Join("&", fields.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
                    uri = new UriBuilder(next) { Query = query }.Uri;
                }
                continue;
            }
            return response;
        }
        throw new InvalidDataException("The provider exceeded its redirect limit.");
    }

    private static Dictionary<string, string> Attributes(string tag) => AttributePattern().Matches(tag).ToDictionary(match => match.Groups[1].Value.ToLowerInvariant(), match => WebUtility.HtmlDecode(match.Groups[3].Value));

    [GeneratedRegex("<form\\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex FormPattern();
    [GeneratedRegex("<input\\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputPattern();
    [GeneratedRegex("([a-zA-Z-]+)\\s*=\\s*(['\"])(.*?)\\2", RegexOptions.Singleline)]
    private static partial Regex AttributePattern();
}

internal sealed class ModDownloadActionException : Exception;
