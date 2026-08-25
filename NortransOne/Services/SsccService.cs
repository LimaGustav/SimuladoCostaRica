using Nortrans.Contracts;

namespace Nortrans.Services;

/// <summary>
/// GS1 Serial Shipping Container Code service. See "Module 2", section 6.
/// </summary>
public sealed class SsccService : ISsccService
{
    public bool IsValidSscc(string? input)
    {
        var value = CodeText.Normalise(input);
        return value.Length == 18 && CodeText.IsDigits(value) && ComputeCheckDigit(value[..17]) == value[17] - '0';
    }

    public int ComputeCheckDigit(string? first17)
    {
        var value = CodeText.Normalise(first17);
        if (value.Length != 17 || !CodeText.IsDigits(value)) return -1;
        var sum = 0;
        for (var i = 16; i >= 0; i--) sum += (value[i] - '0') * ((16 - i) % 2 == 0 ? 3 : 1);
        return (10 - sum % 10) % 10;
    }

    public string FormatSscc(string? input)
    {
        var value = CodeText.Normalise(input);
        if (!IsValidSscc(value)) return string.Empty;
        var payload = value.Substring(1, 16);
        var prefix = TradeData.CompanyPrefixes.Keys.Where(candidate => payload.StartsWith(candidate, StringComparison.Ordinal)).OrderByDescending(candidate => candidate.Length).FirstOrDefault();
        return prefix is null
            ? $"(00) {value[0]} {payload} {value[17]}"
            : $"(00) {value[0]} {prefix} {payload[prefix.Length..]} {value[17]}";
    }

    public string BuildSscc(int extensionDigit, string? companyPrefix, long serialReference)
    {
        var prefix = CodeText.Normalise(companyPrefix);
        if (extensionDigit is < 0 or > 9 || prefix.Length is < 6 or > 10 || !CodeText.IsDigits(prefix) || serialReference < 0) return string.Empty;
        var serialLength = 16 - prefix.Length;
        var serial = serialReference.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (serial.Length > serialLength) return string.Empty;
        var data = $"{extensionDigit}{prefix}{serial.PadLeft(serialLength, '0')}";
        return data + ComputeCheckDigit(data);
    }

    public SsccParts? Parse(string? input)
    {
        var value = CodeText.Normalise(input);
        if (!IsValidSscc(value)) return null;
        var payload = value.Substring(1, 16);
        var prefix = TradeData.CompanyPrefixes.Keys.Where(candidate => payload.StartsWith(candidate, StringComparison.Ordinal)).OrderByDescending(candidate => candidate.Length).FirstOrDefault();
        return prefix is null
            ? new SsccParts(value[0] - '0', payload, string.Empty, value[17] - '0')
            : new SsccParts(value[0] - '0', prefix, payload[prefix.Length..], value[17] - '0');
    }
}
