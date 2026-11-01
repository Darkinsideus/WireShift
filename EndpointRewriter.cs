using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace WireShift;

/// <summary>
/// Работа с WireGuard-конфигами: поиск строки Endpoint и замена host-части
/// с сохранением порта, кодировки и переводов строк.
/// </summary>
public static class EndpointRewriter
{
    // Строка вида:  Endpoint = host:port   [комментарий]
    // addr — вся host:port часть без пробелов, rest — хвост (пробелы, комментарий).
    private static readonly Regex EndpointLine = new(
        @"^(?<prefix>\s*Endpoint\s*=\s*)(?<addr>[^\s#;]+)(?<rest>[^\r\n]*)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex HostNamePattern = new(
        @"^(?=.{1,253}$)([a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?$",
        RegexOptions.Compiled);

    /// <summary>host — без порта; port — с двоеточием (":51820") или пусто.</summary>
    public readonly record struct EndpointParts(string Host, string Port);

    /// <summary>Разбирает "host:port" / "[v6]:port" / "host" на части.</summary>
    public static EndpointParts SplitAddress(string addr)
    {
        if (addr.StartsWith('['))
        {
            int close = addr.IndexOf(']');
            if (close > 0)
            {
                string host = addr[1..close];
                string port = close + 1 < addr.Length ? addr[(close + 1)..] : "";
                return new EndpointParts(host, port);
            }
        }

        int colon = addr.LastIndexOf(':');
        // Ровно одно двоеточие и цифры после него — это порт. Несколько двоеточий — голый IPv6 без порта.
        if (colon > 0 && addr.IndexOf(':') == colon && colon + 1 < addr.Length
            && addr[(colon + 1)..].All(char.IsDigit))
        {
            return new EndpointParts(addr[..colon], addr[colon..]);
        }
        return new EndpointParts(addr, "");
    }

    /// <summary>Ищет первую незакомментированную строку Endpoint. Возвращает null, если не найдена.</summary>
    public static EndpointParts? FindEndpoint(string content)
    {
        var m = EndpointLine.Match(content);
        return m.Success ? SplitAddress(m.Groups["addr"].Value) : null;
    }

    /// <summary>
    /// Заменяет host-часть Endpoint во всех подходящих строках, порт сохраняется.
    /// Возвращает новый текст или null, если менять нечего.
    /// </summary>
    public static string? ReplaceHost(string content, string newHost)
    {
        string formatted = FormatHost(newHost);
        bool changed = false;

        string result = EndpointLine.Replace(content, m =>
        {
            var parts = SplitAddress(m.Groups["addr"].Value);
            string replacement = m.Groups["prefix"].Value + formatted + parts.Port + m.Groups["rest"].Value;
            if (replacement != m.Value) changed = true;
            return replacement;
        });

        return changed ? result : null;
    }

    /// <summary>IPv6 оборачивает в квадратные скобки по правилам WireGuard.</summary>
    public static string FormatHost(string host)
    {
        if (IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
            return $"[{ip}]";
        return host;
    }

    /// <summary>Проверяет, что строка — IPv4, IPv6 или допустимое доменное имя.
    /// errorKey — ключ локализации ("validation_empty" / "validation_bad").</summary>
    public static bool IsValidHost(string host, out string errorKey)
    {
        errorKey = "";
        if (string.IsNullOrWhiteSpace(host))
        {
            errorKey = "validation_empty";
            return false;
        }
        host = host.Trim();
        if (IPAddress.TryParse(host.Trim('[', ']'), out _))
            return true;
        if (HostNamePattern.IsMatch(host))
            return true;
        errorKey = "validation_bad";
        return false;
    }

    /// <summary>Читает файл с учётом BOM (UTF-8/16/32), по умолчанию — UTF-8.</summary>
    public static string ReadFilePreservingEncoding(string path, out Encoding encoding)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            encoding = new UTF8Encoding(true);
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            encoding = Encoding.Unicode;
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            encoding = Encoding.BigEndianUnicode;
        else
            encoding = new UTF8Encoding(false);
        using var reader = new StreamReader(new MemoryStream(bytes), encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Заменяет host в файле. Перед записью создаёт .bak (не затирает существующий).
    /// messageKey — "ok" или ключ локализации причины, почему файл не изменён.
    /// </summary>
    public static bool ApplyToFile(string path, string newHost, out string messageKey)
    {
        string content = ReadFilePreservingEncoding(path, out var encoding);
        string? updated = ReplaceHost(content, newHost);
        if (updated is null)
        {
            messageKey = "msg_no_endpoint";
            return false;
        }

        string backup = path + ".bak";
        if (!File.Exists(backup))
            File.Copy(path, backup);

        File.WriteAllText(path, updated, encoding);
        messageKey = "ok";
        return true;
    }
}
