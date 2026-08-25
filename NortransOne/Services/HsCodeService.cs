using Nortrans.Contracts;

namespace Nortrans.Services;

/// <summary>
/// Harmonized System code service. See "Module 2", section 7.
/// </summary>
public sealed class HsCodeService : IHsCodeService
{
    public bool IsValidHsCode(string? input)
    {
        var value = CodeText.Normalise(input);
        if (value.Length is not (6 or 8 or 10) || !CodeText.IsDigits(value) || !int.TryParse(value[..2], out var chapter) || chapter is < 1 or > 97 or 77) return false;
        return TradeData.Headings.ContainsKey(value[..4]) && TradeData.Headings.ContainsKey(value[..6]);
    }

    public HsBreakdown? Breakdown(string? input)
    {
        var value = CodeText.Normalise(input);
        if (!IsValidHsCode(value) || !int.TryParse(value[..2], out var chapter) || !TradeData.Sections.TryGetValue(chapter, out var section)) return null;
        return new HsBreakdown(value[..2], value[..4], value[..6], value.Length >= 8 ? value.Substring(6, 2) : string.Empty, value.Length == 10 ? value.Substring(8, 2) : string.Empty, section);
    }

    public string FormatDotted(string? input)
    {
        var value = CodeText.Normalise(input);
        if (!IsValidHsCode(value)) return string.Empty;
        return value.Length switch { 6 => $"{value[..4]}.{value[4..]}", 8 => $"{value[..4]}.{value.Substring(4, 2)}.{value[6..]}", _ => $"{value[..4]}.{value.Substring(4, 2)}.{value.Substring(6, 2)}.{value[8..]}" };
    }

    public HsLookup? Lookup(string? input)
    {
        var value = CodeText.Normalise(input);
        if (value.Length < 4 || !CodeText.IsDigits(value)) return null;
        return value.Length >= 6 && TradeData.Headings.TryGetValue(value[..6], out var subheading) ? subheading : TradeData.Headings.TryGetValue(value[..4], out var heading) ? heading : null;
    }

    public IReadOnlyList<HsLookup> Search(string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return [];
        return TradeData.Headings.Values.Where(row => row.Description.Contains(term, StringComparison.OrdinalIgnoreCase)).OrderBy(row => row.Code, StringComparer.Ordinal).ToArray();
    }
}
