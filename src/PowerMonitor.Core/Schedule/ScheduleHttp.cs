using System.Net;
using System.Text.Json;

namespace PowerMonitor.Core.Schedule;

/// <summary>
/// Reads JSON from a fixed set of hosts over https:443 and nothing else. The answer is treated
/// as untrusted: no redirects, a short timeout, a size cap, and failures become <see cref="ScheduleSourceException"/>.
/// </summary>
public sealed class ScheduleHttp : IDisposable
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _client;
    private readonly HashSet<string> _hosts;

    public ScheduleHttp(IEnumerable<string> hosts, HttpMessageHandler? handler = null)
    {
        _hosts = new HashSet<string>(hosts, StringComparer.OrdinalIgnoreCase);
        _client = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout,
            MaxResponseContentBufferSize = MaxBytes
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("PowerMonitor/1.0");
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public bool Allowed(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort && _hosts.Contains(url.Host);

    public async Task<JsonElement> GetJsonAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!Allowed(url))
            throw new ScheduleSourceException($"Refusing to contact {url.Host}.");

        try
        {
            using var response = await _client.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.StatusCode is >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest)
                throw new ScheduleSourceException($"{url.Host} redirected the request.");
            if (!response.IsSuccessStatusCode)
                throw new ScheduleSourceException($"{url.Host} answered {(int)response.StatusCode}.");
            if (response.Content.Headers.ContentLength > MaxBytes)
                throw new ScheduleSourceException($"{url.Host} sent too much data.");

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await body.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaxBytes)
                    throw new ScheduleSourceException($"{url.Host} sent too much data.");
                buffer.Write(chunk, 0, read);
            }

            buffer.Position = 0;
            using var document = JsonDocument.Parse(buffer, new JsonDocumentOptions { MaxDepth = 16 });
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new ScheduleSourceException($"{url.Host} did not answer with JSON.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ScheduleSourceException($"Could not reach {url.Host}: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ScheduleSourceException($"{url.Host} did not answer in time.", ex);
        }
    }

    public void Dispose() => _client.Dispose();
}
