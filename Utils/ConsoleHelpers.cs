using System;
using System.Collections.Generic;

namespace CustomerContactCopilot.Utils;

public static class ConsoleHelpers
{
    public static void WriteColor(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static string TruncateString(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text[..(maxLength - 2)] + "..";
    }

    public static IEnumerable<string> WrapText(string text, int maxWidth)
    {
        var words = text.Split(' ');
        var line = "";
        foreach (var word in words)
        {
            if ((line + " " + word).Length > maxWidth)
            {
                yield return line.Trim();
                line = word;
            }
            else
            {
                line += " " + word;
            }
        }
        if (!string.IsNullOrWhiteSpace(line))
            yield return line.Trim();
    }
}
