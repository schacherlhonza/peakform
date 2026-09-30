using System.Text.RegularExpressions;

namespace TrainCoach.Application.Integrations.StravaArchive;

/// <summary>
/// The only URLs the server will ever fetch for an archive import (SSRF guard). The emailed
/// "Download Archive" button is a click-tracking link on <c>email.strava.com/ls/click</c> that
/// 302-redirects to a presigned, 7-day S3 URL under the <c>strava.portability</c> bucket:
/// <c>https://s3.amazonaws.com/strava.portability/athlete/{athleteId}/export/.../export_{athleteId}.zip?X-Amz-...</c>.
/// Both forms are accepted as input; anything else — including a redirect elsewhere — is refused.
/// </summary>
public static partial class StravaArchiveLink
{
    public static bool IsAllowedEntryUrl(Uri url) => IsTrackingUrl(url) || IsArchiveUrl(url);

    public static bool IsTrackingUrl(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps
        && url.IsDefaultPort
        && string.Equals(url.Host, "email.strava.com", StringComparison.OrdinalIgnoreCase)
        && url.AbsolutePath.StartsWith("/ls/click", StringComparison.Ordinal);

    public static bool IsArchiveUrl(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps
        && url.IsDefaultPort
        && S3HostRegex().IsMatch(url.Host)
        && ArchivePathRegex().IsMatch(url.AbsolutePath);

    /// <summary>The Strava athlete id embedded in the S3 path, or null.</summary>
    public static string? AthleteIdFromArchiveUrl(Uri url)
    {
        var match = ArchivePathRegex().Match(url.AbsolutePath);
        return match.Success ? match.Groups["id"].Value : null;
    }

    /// <summary>The Strava athlete id from the default download file name <c>export_{id}.zip</c>, or null.</summary>
    public static string? AthleteIdFromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }
        var match = FileNameRegex().Match(Path.GetFileName(fileName));
        return match.Success ? match.Groups["id"].Value : null;
    }

    public static bool TryParse(string? value, out Uri url)
    {
        url = null!;
        return !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value.Trim(), UriKind.Absolute, out url!)
            && IsAllowedEntryUrl(url);
    }

    // Path-style S3 host, global or regional (s3.eu-west-1.amazonaws.com / s3-eu-west-1.amazonaws.com).
    [GeneratedRegex(@"^s3([.-][a-z0-9-]+)?\.amazonaws\.com$", RegexOptions.IgnoreCase)]
    private static partial Regex S3HostRegex();

    [GeneratedRegex(@"^/strava\.portability/athlete/(?<id>\d+)/[^?#]+\.zip$", RegexOptions.IgnoreCase)]
    private static partial Regex ArchivePathRegex();

    [GeneratedRegex(@"^export_(?<id>\d+)\.zip$", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameRegex();
}
