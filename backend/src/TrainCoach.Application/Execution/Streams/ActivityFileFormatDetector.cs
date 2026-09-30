using System.IO.Compression;
using System.Text;
using TrainCoach.Application.Integrations.StravaArchive;

namespace TrainCoach.Application.Execution.Streams;

/// <summary>Recognizes a downloaded activity file by content (providers don't reliably say what
/// they send): gzip is unwrapped first, then FIT by its header signature, GPX/TCX by root element.</summary>
public static class ActivityFileFormatDetector
{
    private const long MaxDecompressedBytes = 64L * 1024 * 1024;

    public static (byte[] Content, ActivityFileFormat Format)? Detect(byte[] content)
    {
        if (content.Length >= 2 && content[0] == 0x1F && content[1] == 0x8B)
        {
            using var gzip = new GZipStream(new MemoryStream(content), CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = gzip.Read(buffer)) > 0)
            {
                if (output.Length + read > MaxDecompressedBytes)
                {
                    return null;
                }
                output.Write(buffer, 0, read);
            }
            content = output.ToArray();
        }

        // FIT header: byte 0 = header size (12 or 14), bytes 8-11 = ".FIT".
        if (content.Length >= 12 && content[0] is 12 or 14 && content[8] == '.' && content[9] == 'F' && content[10] == 'I' && content[11] == 'T')
        {
            return (content, ActivityFileFormat.Fit);
        }

        var head = Encoding.UTF8.GetString(content, 0, Math.Min(content.Length, 2048));
        if (head.Contains("<gpx", StringComparison.OrdinalIgnoreCase))
        {
            return (content, ActivityFileFormat.Gpx);
        }
        if (head.Contains("<TrainingCenterDatabase", StringComparison.OrdinalIgnoreCase))
        {
            return (content, ActivityFileFormat.Tcx);
        }
        return null;
    }
}
