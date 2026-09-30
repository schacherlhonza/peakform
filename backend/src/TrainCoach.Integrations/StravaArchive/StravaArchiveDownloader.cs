using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations.StravaArchive;

namespace TrainCoach.Integrations.StravaArchive;

/// <summary>
/// Downloads a Strava archive from the emailed link. Redirects are followed by hand (the named
/// HttpClient has auto-redirect off) so every hop is checked against <see cref="StravaArchiveLink"/>:
/// the click-tracking link may only lead to the S3 archive URL, nowhere else. The URL itself is a
/// bearer secret and is never logged or put into an exception message.
/// </summary>
public class StravaArchiveDownloader(IHttpClientFactory httpClientFactory) : IStravaArchiveDownloader
{
    public const string HttpClientName = nameof(StravaArchiveDownloader);
    private const int MaxRedirects = 3;

    public async Task<StravaArchiveDownloadResult> DownloadAsync(
        Uri url, Stream destination, long maxBytes, Func<long, long?, Task>? onProgress, CancellationToken cancellationToken = default)
    {
        if (!StravaArchiveLink.IsAllowedEntryUrl(url))
        {
            throw new BusinessRuleException("Odkaz není odkazem na archiv ze Stravy.");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var current = url;
        for (var hop = 0; ; hop++)
        {
            // GET, not HEAD: the presigned S3 URL is signed for GET only (HEAD returns 403).
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (IsRedirect(response))
            {
                var location = response.Headers.Location;
                if (location is null || hop >= MaxRedirects)
                {
                    throw new BusinessRuleException("Odkaz na archiv ze Stravy nevede na soubor ke stažení.");
                }
                var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                if (!StravaArchiveLink.IsArchiveUrl(next) && !StravaArchiveLink.IsTrackingUrl(next))
                {
                    throw new BusinessRuleException("Odkaz přesměrovává mimo úložiště archivů Stravy – z bezpečnostních důvodů ho nestáhneme.");
                }
                current = next;
                continue;
            }

            if (!StravaArchiveLink.IsArchiveUrl(current))
            {
                // A tracking link that answered without redirecting — usually an expired/unknown link page.
                throw new BusinessRuleException("Odkaz z e-mailu už nevede na archiv. Vyžádejte si na Stravě nový export.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadSmallBodyAsync(response, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden && body.Contains("Request has expired", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessRuleException("Odkaz na archiv vypršel (Strava ho drží platný 7 dní). Vyžádejte si na Stravě nový export.");
                }
                throw new BusinessRuleException($"Archiv se nepodařilo stáhnout (HTTP {(int)response.StatusCode}). Zkuste to znovu, případně archiv stáhněte a nahrajte ručně.");
            }

            var total = response.Content.Headers.ContentLength;
            if (total > maxBytes)
            {
                throw new BusinessRuleException($"Archiv je větší než povolený limit {maxBytes / (1024 * 1024)} MB.");
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                downloaded += read;
                if (downloaded > maxBytes)
                {
                    throw new BusinessRuleException($"Archiv je větší než povolený limit {maxBytes / (1024 * 1024)} MB.");
                }
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                if (onProgress is not null)
                {
                    await onProgress(downloaded, total);
                }
            }
            await destination.FlushAsync(cancellationToken);

            return new StravaArchiveDownloadResult(StravaArchiveLink.AthleteIdFromArchiveUrl(current), downloaded);
        }
    }

    private static bool IsRedirect(HttpResponseMessage response) => (int)response.StatusCode is >= 300 and < 400;

    private static async Task<string> ReadSmallBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[4096];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }
}
