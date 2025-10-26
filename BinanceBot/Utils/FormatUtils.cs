namespace Bot.Utils;

using System.Globalization;

public static class FormatUtils
{
    public static string PctStr(decimal r) => (r * 100m).ToString("0.###", CultureInfo.InvariantCulture);
    public static string Num(string s) => s.Replace(',', '.');
}
