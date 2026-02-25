using System;
using System.IO;
using CustomerContactCopilot.Models;

namespace CustomerContactCopilot.Utils;

public static class EmailParser
{
    public static CustomerEmail ParseEmailFile(string content, string filePath)
    {
        var email = new CustomerEmail
        {
            Id = Path.GetFileNameWithoutExtension(filePath)
        };

        var lines = content.Split('\n');
        var bodyStarted = false;
        var bodyBuilder = new System.Text.StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (!bodyStarted)
            {
                if (line.StartsWith("From:", StringComparison.OrdinalIgnoreCase))
                {
                    email.FromAddress = line.Substring(5).Trim();
                }
                else if (line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
                {
                    email.Subject = line.Substring(8).Trim();
                }
                else if (line.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
                {
                    var dateStr = line.Substring(5).Trim();
                    if (DateTime.TryParse(dateStr, out var date))
                    {
                        email.ReceivedAt = date;
                    }
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    bodyStarted = true;
                }
            }
            else
            {
                bodyBuilder.AppendLine(rawLine);
            }
        }

        email.Body = bodyBuilder.ToString().Trim();
        return email;
    }
}
