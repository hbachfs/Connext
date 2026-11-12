namespace Connext.Shared;

public static class PhoneNumberNormalizer
{
    public static string Normalize(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new FormatException("Số điện thoại không hợp lệ.");
        var value = phoneNumber.Trim();
        if (value.Count(character => character == '+') > 1 ||
            (value.Contains('+') && value[0] != '+'))
            throw new FormatException("Số điện thoại không hợp lệ.");

        if (value.Any(character =>
                !char.IsDigit(character) && character is not '+' and not ' ' and not '-' and not '(' and not ')' and not '.'))
            throw new FormatException("Số điện thoại không hợp lệ.");

        var digits = new string(value.Where(char.IsDigit).ToArray());
        var hasCountryPrefix = value[0] == '+' || digits.StartsWith("84", StringComparison.Ordinal);
        if (digits.StartsWith("0084", StringComparison.Ordinal))
        {
            digits = digits[2..];
            hasCountryPrefix = true;
        }
        else if (digits.StartsWith('0') && digits.Length == 10)
            digits = $"84{digits[1..]}";
        else if (digits.Length == 9 && !hasCountryPrefix)
            digits = $"84{digits}";

        if (!digits.StartsWith("84", StringComparison.Ordinal) ||
            digits.Length != 11 ||
            digits[2] is not ('3' or '5' or '7' or '8' or '9'))
            throw new FormatException("Số điện thoại Việt Nam không hợp lệ.");

        return $"+{digits}";
    }

    public static bool TryNormalize(string phoneNumber, out string normalized)
    {
        try
        {
            normalized = Normalize(phoneNumber);
            return true;
        }
        catch (FormatException)
        {
            normalized = string.Empty;
            return false;
        }
        catch (ArgumentException)
        {
            normalized = string.Empty;
            return false;
        }
    }
}
