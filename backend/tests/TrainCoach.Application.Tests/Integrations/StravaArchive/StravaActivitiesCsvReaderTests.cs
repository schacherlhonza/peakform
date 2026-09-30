using FluentAssertions;
using TrainCoach.Application.Integrations.StravaArchive;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Tests.Integrations.StravaArchive;

public class StravaActivitiesCsvReaderTests
{
    // Synthetic rows in the real export's layout: display block (0-14, localized units) then the
    // machine block (15+), with Elapsed Time / Distance / Max Heart Rate repeated.
    private const string CzechHeader =
        "ID aktivity,Datum aktivity,Název aktivity,Typ aktivity,Popis aktivity,Uplynulý čas,Vzdálenost,Maximální tepová frekvence,Relativní úsilí,Dojíždění,Soukromá poznámka k aktivitě,Vybavení na aktivitu,Název souboru,Hmotnost sportovce,Hmotnost kola,"
        + "Uplynulý čas,Aktivní čas,Vzdálenost,Maximální rychlost,Průměrná rychlost,Nastoupaná výška,Naklesaná výška,Nejnižší nadmořská výška,Nejvyšší nadmořská výška,Maximální sklon,Průměrný sklon,Průměrný pozitivní sklon,Průměrný záporný sklon,Maximální kadence,Průměrná kadence,Maximální tepová frekvence,Průměrná tepová frekvence,Maximální výkon ve wattech,Průměrný výkon ve wattech,Kalorie";

    private const string EnglishHeader =
        "Activity ID,Activity Date,Activity Name,Activity Type,Activity Description,Elapsed Time,Distance,Max Heart Rate,Relative Effort,Commute,Activity Private Note,Activity Gear,Filename,Athlete Weight,Bike Weight,"
        + "Elapsed Time,Moving Time,Distance,Max Speed,Average Speed,Elevation Gain,Elevation Loss,Elevation Low,Elevation High,Max Grade,Average Grade,Average Positive Grade,Average Negative Grade,Max Cadence,Average Cadence,Max Heart Rate,Average Heart Rate,Max Watts,Average Watts,Calories";

    private static List<StravaCsvActivity> Read(string csv) => StravaActivitiesCsvReader.Read(new StringReader(csv)).ToList();

    [Fact]
    public void Czech_export_uses_machine_unit_columns_and_utc_date()
    {
        var csv = CzechHeader + "\n"
            + "20061183385,\"6. 9. 2026 11:18:40\",Odpolední jízda,Jízda,,7052,\"33,22\",140.0,,,,,activities/21204285641.fit.gz,,,"
            + "7052.0,6490.0,33227.2,12.2,5.12,480.0,,,,,,,,,,140.0,109.0,,98.0,863.0\n";

        var row = Read(csv).Single();

        row.Error.Should().BeNull();
        row.ActivityId.Should().Be("20061183385");
        row.StartedAtUtc.Should().Be(new DateTime(2026, 9, 6, 11, 18, 40, DateTimeKind.Utc));
        row.TypeLabel.Should().Be("Jízda");
        row.FileName.Should().Be("activities/21204285641.fit.gz");
        row.ElapsedSeconds.Should().Be(7052.0m);
        row.MovingSeconds.Should().Be(6490.0m);
        row.DistanceMeters.Should().Be(33227.2m, "the second Distance column holds meters, the first localized km");
        row.MaxHeartRate.Should().Be(140m);
        row.AverageHeartRate.Should().Be(109m);
        row.AverageWatts.Should().Be(98m);
        row.Calories.Should().Be(863m);
        row.RawValues.Should().ContainKey("Vzdálenost").And.ContainKey("Vzdálenost #2");
    }

    [Fact]
    public void English_export_parses_with_english_headers_and_date_format()
    {
        var csv = EnglishHeader + "\n"
            + "1012577285,\"May 15, 2017, 5:52:52 PM\",Evening Run,Run,,1617,4.11,177.0,,,,,activities/1115181649.fit.gz,,,"
            + "1617.0,1617.0,4110.1,3.7,,96.0,,,,,,,,,75.0,177.0,161.0,,,\n";

        var row = Read(csv).Single();

        row.StartedAtUtc.Should().Be(new DateTime(2017, 5, 15, 17, 52, 52, DateTimeKind.Utc));
        row.TypeLabel.Should().Be("Run");
        row.DistanceMeters.Should().Be(4110.1m);
        row.AverageSpeedMetersPerSecond.Should().BeNull();
        row.Calories.Should().BeNull();
    }

    [Fact]
    public void Quoted_multiline_description_with_commas_and_quotes_stays_one_row()
    {
        var csv = CzechHeader + "\n"
            + "1,\"1. 1. 2024 8:00:00\",Běh,Běh,\"první řádek, s čárkou\na \"\"uvozovky\"\"\",60,\"0,20\",,,,,,activities/1.gpx,,,60.0,60.0,200.0,,,,,,,,,,,,,,,,,\n"
            + "2,\"2. 1. 2024 8:00:00\",Běh,Běh,,60,\"0,20\",,,,,,activities/2.gpx,,,60.0,60.0,200.0,,,,,,,,,,,,,,,,,\n";

        var rows = Read(csv);

        rows.Should().HaveCount(2);
        rows[0].RawValues["Popis aktivity"].Should().Be("první řádek, s čárkou\na \"uvozovky\"");
        rows[1].ActivityId.Should().Be("2");
    }

    [Fact]
    public void Row_without_numeric_id_is_reported_as_error()
    {
        var csv = CzechHeader + "\nabc,\"1. 1. 2024 8:00:00\",x,Běh,,60,,,,,,,,,,60,60,,,,,,,,,,,,,,,,,,\n";

        Read(csv).Single().Error.Should().NotBeNull();
    }

    [Fact]
    public void File_that_is_not_a_strava_export_is_rejected()
    {
        var act = () => Read("a,b\n1,2\n");

        act.Should().Throw<StravaArchiveFormatException>();
    }

    [Theory]
    [InlineData("Běh", SportType.Running)]
    [InlineData("Virtual Ride", SportType.Cycling)]
    [InlineData("Posilování", SportType.Strength)]
    [InlineData("Workout", SportType.Strength)]
    [InlineData("Turistika", SportType.Other)]
    public void Localized_sport_labels_map_like_the_api_adapter(string label, SportType expected)
    {
        StravaSportTypeMapper.FromLabel(label).Should().Be(expected);
    }

    [Fact]
    public void Unknown_label_falls_back_to_file_sport()
    {
        StravaSportTypeMapper.FromLabel("Kitesurfing").Should().BeNull();
        StravaSportTypeMapper.FromFileSport("Running").Should().Be(SportType.Running);
        StravaSportTypeMapper.FromFileSport("biking").Should().Be(SportType.Cycling);
    }
}
