using Connext.Shared;

namespace Connext.Tests;

public class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("0901234567", "+84901234567")]
    [InlineData("+84 90 1234 567", "+84901234567")]
    [InlineData("84901234567", "+84901234567")]
    [InlineData("0084901234567", "+84901234567")]
    public void Normalize_EquivalentVietnameseNumbers_ReturnsCanonicalValue(
        string input,
        string expected)
    {
        Assert.Equal(expected, PhoneNumberNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567890")]
    [InlineData("+849012345")]
    [InlineData("0901234567abc")]
    public void Normalize_InvalidPhoneNumber_Throws(string input)
    {
        Assert.Throws<FormatException>(() => PhoneNumberNormalizer.Normalize(input));
    }
}
