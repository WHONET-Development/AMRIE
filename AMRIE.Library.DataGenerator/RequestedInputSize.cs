using System.Globalization;

namespace AMRIE.Library.DataGenerator;

internal readonly record struct RequestedInputSize(long Quantity, string Unit, long ByteCount)
{
    public string DisplayText => string.Concat(Quantity.ToString(CultureInfo.InvariantCulture), Unit);

    public static bool TryParse(string? value, out RequestedInputSize requestedSize)
    {
        requestedSize = default;
        if (string.IsNullOrEmpty(value) || value.Length <= 2)
            return false;

        string unit = value[^2..];
        long multiplier;
        if (unit.Equals("MB", StringComparison.OrdinalIgnoreCase))
            multiplier = 1024L * 1024L;
        else if (unit.Equals("GB", StringComparison.OrdinalIgnoreCase))
            multiplier = 1024L * 1024L * 1024L;
        else
            return false;

        ReadOnlySpan<char> quantityText = value.AsSpan(0, value.Length - 2);
        if (!long.TryParse(quantityText, NumberStyles.None, CultureInfo.InvariantCulture, out long quantity)
            || quantity <= 0
            || quantity > long.MaxValue / multiplier)
            return false;

        requestedSize = new RequestedInputSize(quantity, unit.ToUpperInvariant(), quantity * multiplier);
        return true;
    }
}
