using Taskly.SharedKernel.Domain;

namespace Taskly.UnitTests.SharedKernel;

public sealed class TextRulesTests
{
    [Fact]
    public void TrimsWhitespaceSeparatorsAndByteOrderMarks()
    {
        Assert.Equal("value", TextRules.Trim("  \t\n value \u00A0\uFEFF"));
        Assert.Equal(string.Empty, TextRules.Trim(" \u2003 "));
    }

    [Fact]
    public void ParsesTrimmedTextWithinBounds()
    {
        Assert.Equal("abc", TextRules.Parse("  abc ", "Label", 1, 3));
    }

    [Fact]
    public void RejectsNonStringValues()
    {
        foreach (var value in new object?[] { null, 1, true, new object() })
        {
            var error = Assert.Throws<ValidationException>(() => TextRules.Parse(value, "Label", 1, 3));
            Assert.Equal("Label is required", error.Message);
        }
    }

    [Fact]
    public void RejectsTextOutsideBounds()
    {
        var tooShort = Assert.Throws<ValidationException>(() => TextRules.Parse("  ", "Label", 1, 3));
        var tooLong = Assert.Throws<ValidationException>(() => TextRules.Parse("abcd", "Label", 1, 3));

        Assert.Equal("Label must be between 1 and 3 characters", tooShort.Message);
        Assert.Equal("Label must be between 1 and 3 characters", tooLong.Message);
    }

    [Fact]
    public void AllowsEmptyTextWhenTheMinimumIsZero()
    {
        Assert.Equal(string.Empty, TextRules.Parse("   ", "Label", 0, 3));
    }
}
