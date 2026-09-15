using FluentAssertions;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;
using Xunit;

namespace TrainCoach.Application.Tests.Integrations;

public class CsvImportParserTests
{
    [Fact]
    public void Parse_StandardCzechTemplate_ParsesValidRows()
    {
        const string csv = "Datum,Sport,DobaMinuty,VzdalenostKm,PrevyseniM,PrumernyTep,Poznamka\n" +
                            "2026-01-06,Běh,45,8,120,150,Lehký běh\n";

        var rows = CsvImportParser.Parse(csv);

        rows.Should().HaveCount(1);
        var row = rows[0];
        row.Status.Should().Be(ImportRowStatus.Valid);
        row.Date.Should().Be(new DateOnly(2026, 1, 6));
        row.Sport.Should().Be(SportType.Running);
        row.DurationMinutes.Should().Be(45);
        row.DistanceKm.Should().Be(8);
        row.ElevationM.Should().Be(120);
        row.AverageHeartRate.Should().Be(150);
        row.Notes.Should().Be("Lehký běh");
    }

    [Fact]
    public void Parse_MissingDate_IsFlaggedAsError()
    {
        const string csv = "Datum,Sport,VzdalenostKm\n,Běh,5\n";

        var rows = CsvImportParser.Parse(csv);

        rows[0].Status.Should().Be(ImportRowStatus.Error);
        rows[0].Messages.Should().Contain(m => m.Contains("datum", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_UnrecognizedSport_DefaultsToRunningWithWarning()
    {
        const string csv = "Datum,Sport,VzdalenostKm\n2026-01-06,Nějaký neznámý sport,5\n";

        var rows = CsvImportParser.Parse(csv);

        rows[0].Sport.Should().Be(SportType.Running);
        rows[0].Status.Should().Be(ImportRowStatus.Warning);
    }

    [Fact]
    public void Parse_MissingDistanceAndDuration_IsError()
    {
        const string csv = "Datum,Sport\n2026-01-06,Běh\n";

        var rows = CsvImportParser.Parse(csv);

        rows[0].Status.Should().Be(ImportRowStatus.Error);
    }

    [Fact]
    public void Parse_RestDay_DoesNotRequireDistanceOrDuration()
    {
        const string csv = "Datum,Sport\n2026-01-06,Odpočinek\n";

        var rows = CsvImportParser.Parse(csv);

        rows[0].Sport.Should().Be(SportType.Rest);
        rows[0].Status.Should().Be(ImportRowStatus.Valid);
    }

    [Fact]
    public void Parse_HandlesHeaderAliasesCaseAndDiacriticsInsensitively()
    {
        const string csv = "datum;SPORT;Vzdálenost (km)\n2026-01-06;běh;10\n";

        var rows = CsvImportParser.Parse(csv);

        rows.Should().HaveCount(1);
        rows[0].DistanceKm.Should().Be(10);
        rows[0].Sport.Should().Be(SportType.Running);
    }

    [Fact]
    public void Parse_CommaDecimalSeparator_IsAccepted()
    {
        const string csv = "Datum,Sport,VzdalenostKm\n2026-01-06,Běh,7,5\n";
        // Note: with a ';' separator this ambiguity wouldn't occur; with ',' as both the CSV
        // separator and decimal separator the distance column becomes "7" — verify the simpler,
        // unambiguous semicolon-separated case decodes a comma decimal correctly instead.
        const string csvSemicolon = "Datum;Sport;VzdalenostKm\n2026-01-06;Běh;7,5\n";

        var rows = CsvImportParser.Parse(csvSemicolon);

        rows[0].DistanceKm.Should().Be(7.5m);
    }
}
