// CsvTests — the three things that break naive splitting, and the row numbering
// an error message depends on.
//
// Use:  dotnet test
// Edit: every case here came from what a real dealer export looks like. A comma
//       in an address, a quote in a trading name, a line break inside a field.
//       If any of these regress, an import silently puts the wrong value in the
//       wrong column, which is worse than refusing the file.

using FluentAssertions;
using OpenDealer360.DataMigration;

namespace OpenDealer360.UnitTests;

public sealed class CsvTests
{
    [Fact]
    public void Reads_a_plain_file()
    {
        var document = Csv.Read("vin,make,model\n1HG,Toyota,RAV4\n2FA,Ford,F-150");

        document.Header.Should().Equal("vin", "make", "model");
        document.Rows.Should().HaveCount(2);
        document.Rows[0].Fields.Should().Equal("1HG", "Toyota", "RAV4");
    }

    [Fact]
    public void Keeps_a_comma_that_is_inside_quotes()
    {
        var document = Csv.Read("externalid,lastname,addressline1\nA1,Smith,\"12 High Street, Apt 4\"");

        document.Rows[0].Fields.Should().Equal("A1", "Smith", "12 High Street, Apt 4");
    }

    [Fact]
    public void Turns_a_doubled_quote_into_one()
    {
        var document = Csv.Read("externalid,lastname\nA1,\"Bob \"\"Big Bob\"\" Motors\"");

        document.Rows[0].Fields[1].Should().Be("Bob \"Big Bob\" Motors");
    }

    [Fact]
    public void Keeps_a_line_break_that_is_inside_quotes()
    {
        var document = Csv.Read("externalid,addressline1\nA1,\"12 High Street\nSecond line\"");

        document.Rows.Should().HaveCount(1, because: "that is one record, not two");
        document.Rows[0].Fields[1].Should().Be("12 High Street\nSecond line");
    }

    [Fact]
    public void Numbers_rows_the_way_a_spreadsheet_does()
    {
        var document = Csv.Read("vin\nA\nB");

        // The header is line 1, so the first record is line 2. An error saying
        // "row 3" has to point at what row 3 looks like on their screen.
        document.Rows[0].Number.Should().Be(2);
        document.Rows[1].Number.Should().Be(3);
    }

    [Fact]
    public void Keeps_each_row_exactly_as_it_arrived()
    {
        const string Raw = "A1,\"Smith, J\",\"quoted\"";
        var document = Csv.Read($"externalid,lastname,note\n{Raw}");

        // The migration workflow forbids fixing an exception by editing the
        // source, so what we received has to survive verbatim.
        document.Rows[0].Raw.Should().Be(Raw);
    }

    [Fact]
    public void Handles_windows_line_endings()
    {
        var document = Csv.Read("vin,make\r\n1HG,Toyota\r\n2FA,Ford\r\n");

        document.Rows.Should().HaveCount(2);
        document.Rows[1].Fields.Should().Equal("2FA", "Ford");
    }

    [Fact]
    public void Ignores_blank_lines_in_the_middle()
    {
        var document = Csv.Read("vin\nA\n\nB\n");

        document.Rows.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("Model Year")]
    [InlineData("model_year")]
    [InlineData("MODELYEAR")]
    public void Treats_column_names_as_the_same_however_they_are_spelled(string spelling)
    {
        var document = Csv.Read($"vin,{spelling}\n1HG,2021");

        document.Rows[0].IntField(document.Header, "modelyear").Should().Be(2021);
    }

    [Fact]
    public void Reports_an_empty_document_as_having_no_rows()
    {
        Csv.Read(string.Empty).Rows.Should().BeEmpty();
        Csv.Read("vin,make").Rows.Should().BeEmpty(because: "a header alone is not data");
    }

    [Fact]
    public void Reads_an_absent_or_empty_column_as_nothing()
    {
        var document = Csv.Read("vin,trim\n1HG,");

        document.Rows[0].Field(document.Header, "trim").Should().BeNull();
        document.Rows[0].Field(document.Header, "colour").Should().BeNull();
    }

    [Fact]
    public void Reads_a_number_that_is_not_a_number_as_nothing()
    {
        var document = Csv.Read("vin,modelyear\n1HG,two thousand");

        // Null rather than zero: a car from year 0 would be imported silently.
        document.Rows[0].IntField(document.Header, "modelyear").Should().BeNull();
    }

    [Fact]
    public void Names_the_columns_a_kind_needs_when_they_are_missing()
    {
        var header = Csv.Read("vin,make\n1HG,Toyota").Header;

        ImportRunner.MissingColumns(ImportKind.Vehicles, header)
            .Should().BeEquivalentTo(["modelyear", "model"]);

        ImportRunner.MissingColumns(ImportKind.Vehicles, Csv.Read("vin,modelyear,make,model\nx,1,y,z").Header)
            .Should().BeEmpty();
    }
}
