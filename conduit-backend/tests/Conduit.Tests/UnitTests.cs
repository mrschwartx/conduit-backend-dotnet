using Conduit.Application.Common;
using Conduit.Application.Identity;
using Conduit.Domain.Articles;
using Xunit;

namespace Conduit.Tests;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Hello, World!", "hello-world")]
    [InlineData("Crème Brûlée", "creme-brulee")]
    [InlineData("  --Already--Slugged--  ", "already-slugged")]
    [InlineData("!!!", "article")]
    public void Slugify(string title, string expected) => Assert.Equal(expected, SlugGenerator.Slugify(title));

    [Fact] public void Feed_is_reserved() => Assert.True(SlugGenerator.IsReserved("feed"));

    [Fact]
    public void Long_titles_and_suffixes_stay_within_80_chars()
    {
        var slug = SlugGenerator.Slugify(new string('a', 200));
        Assert.Equal(80, slug.Length);
        Assert.True(SlugGenerator.WithSuffix(slug, 12).Length <= 80);
        Assert.EndsWith("-12", SlugGenerator.WithSuffix(slug, 12));
    }
}

public class ValidatorTests
{
    private static Optional<string?> V(string? s) => Optional<string?>.Of(s);

    [Fact]
    public void Register_reports_all_failing_fields_with_camelCase_keys()
    {
        var result = RegisterValidator.Instance.Validate(new RegisterInput(V("ab"), V("not-an-email"), V("short")));
        Assert.Contains(result.Errors, e => e.PropertyName == "username" && e.ErrorMessage == "is too short (minimum is 3 characters)");
        Assert.Contains(result.Errors, e => e.PropertyName == "email" && e.ErrorMessage == "is invalid");
        Assert.Contains(result.Errors, e => e.PropertyName == "password" && e.ErrorMessage.StartsWith("is too short"));
    }

    [Fact]
    public void Register_accepts_valid_input() =>
        Assert.True(RegisterValidator.Instance.Validate(new RegisterInput(V("alice_1"), V("a@b.co"), V("password123"))).IsValid);

    [Fact]
    public void Update_ignores_absent_fields_and_allows_null_bio()
    {
        var input = new UpdateUserInput(default, default, default, V(null), default);
        Assert.True(UpdateUserValidator.Instance.Validate(input).IsValid);
    }

    [Fact]
    public void Username_length_counts_characters_not_utf16_units()
    {
        // 31 emoji = 62 UTF-16 units, but also fails the pattern; the length message must still say "too long"
        var result = RegisterValidator.Instance.Validate(new RegisterInput(V(string.Concat(Enumerable.Repeat("😀", 31))), V("a@b.co"), V("password123")));
        Assert.Contains(result.Errors, e => e.PropertyName == "username" && e.ErrorMessage.StartsWith("is too long"));
    }
}
