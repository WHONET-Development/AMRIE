using AMR_Engine;

namespace Interpretation_Engine.Tests;

public class DelimitedTextTests
{
    [Fact]
    public void SplitLineAndToLineRoundTripValuesWithQuotesDelimitersAndWhitespace()
    {
        string[] values = ["", "plain", "two words", " leading ", "a\tb", "a\"b", "\"", "tail "];

        string line = IO_Library.ToLine(values, '\t');

        Assert.Equal(values, IO_Library.SplitLine(line, '\t'));
    }

    [Fact]
    public void SplitLineHandlesQuotedSeparatorsAndTrailingEmptyFields()
    {
        Assert.Equal(new[] { "first", "second,part", "" },
            IO_Library.SplitLine("first,\"second,part\",", ','));
    }

    [Fact]
    public void SplitLinePreservesAQuotedValueWithoutADelimiter()
    {
        Assert.Equal(new[] { "a\"b" }, IO_Library.SplitLine("\"a\"\"b\"", ','));
    }
}