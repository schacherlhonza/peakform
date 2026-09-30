using System.IO.Compression;
using System.Text;
using FluentAssertions;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Application.Integrations.StravaArchive;

namespace TrainCoach.Application.Tests.Execution;

public class ActivityFileFormatDetectorTests
{
    private static readonly byte[] FitHeader = [14, 0x20, 0, 0, 0, 0, 0, 0, (byte)'.', (byte)'F', (byte)'I', (byte)'T', 0, 0];

    private static byte[] Gzip(byte[] content)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(content);
        }
        return buffer.ToArray();
    }

    [Fact]
    public void Recognizes_fit_by_header_even_when_gzipped()
    {
        ActivityFileFormatDetector.Detect(FitHeader)!.Value.Format.Should().Be(ActivityFileFormat.Fit);

        var detected = ActivityFileFormatDetector.Detect(Gzip(FitHeader))!.Value;
        detected.Format.Should().Be(ActivityFileFormat.Fit);
        detected.Content.Should().Equal(FitHeader);
    }

    [Theory]
    [InlineData("<?xml version=\"1.0\"?><gpx version=\"1.1\"></gpx>", ActivityFileFormat.Gpx)]
    [InlineData("<?xml version=\"1.0\"?><TrainingCenterDatabase></TrainingCenterDatabase>", ActivityFileFormat.Tcx)]
    public void Recognizes_xml_formats_by_root_element(string xml, ActivityFileFormat expected)
    {
        ActivityFileFormatDetector.Detect(Encoding.UTF8.GetBytes(xml))!.Value.Format.Should().Be(expected);
    }

    [Fact]
    public void Unknown_content_is_not_an_activity_file()
    {
        ActivityFileFormatDetector.Detect(Encoding.UTF8.GetBytes("{\"error\":\"not found\"}")).Should().BeNull();
    }
}
