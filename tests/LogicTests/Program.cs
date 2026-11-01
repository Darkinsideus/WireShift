using WireShift;

int failed = 0;

void Check(string name, bool cond)
{
    Console.WriteLine((cond ? "PASS" : "FAIL") + "  " + name);
    if (!cond) failed++;
}

// 1. Обычный IPv4 + порт: host меняется, порт и остальной файл сохраняются
string conf1 = "[Interface]\nPrivateKey = ABC\n\n[Peer]\nPublicKey = XYZ\nEndpoint = 203.0.113.5:51820\nAllowedIPs = 0.0.0.0/0\n";
var r1 = EndpointRewriter.ReplaceHost(conf1, "10.0.0.1");
Check("IPv4 host заменён, порт сохранён",
    r1 != null && r1.Contains("Endpoint = 10.0.0.1:51820") && r1.Contains("PublicKey = XYZ"));

// 2. Тот же host — изменений нет (вернёт null)
Check("Тот же host → без изменений", EndpointRewriter.ReplaceHost(conf1, "203.0.113.5") is null);

// 3. IPv6 endpoint в скобках
string conf2 = "[Peer]\nEndpoint = [2001:db8::1]:51820\n";
var r2 = EndpointRewriter.ReplaceHost(conf2, "192.0.2.10");
Check("IPv6-источник → IPv4, порт сохранён", r2 != null && r2.Contains("Endpoint = 192.0.2.10:51820"));

// 4. Новый host — IPv6: должен обернуться в скобки
var r3 = EndpointRewriter.ReplaceHost(conf1, "2001:db8::99");
Check("Новый IPv6 обёрнут в []", r3 != null && r3.Contains("Endpoint = [2001:db8::99]:51820"));

// 5. Доменное имя как новый host
var r4 = EndpointRewriter.ReplaceHost(conf1, "vpn.example.com");
Check("Домен как новый host", r4 != null && r4.Contains("Endpoint = vpn.example.com:51820"));

// 6. Закомментированная строка не трогается
string conf3 = "[Peer]\n# Endpoint = 1.1.1.1:1234\nEndpoint = 5.6.7.8:51820\n";
var r5 = EndpointRewriter.ReplaceHost(conf3, "9.9.9.9");
Check("Комментарий не затронут",
    r5 != null && r5.Contains("# Endpoint = 1.1.1.1:1234") && r5.Contains("Endpoint = 9.9.9.9:51820"));

// 7. Endpoint без порта
string conf4 = "[Peer]\nEndpoint = 1.2.3.4\n";
var r6 = EndpointRewriter.ReplaceHost(conf4, "9.9.9.9");
Check("Endpoint без порта", r6 != null && r6.Contains("Endpoint = 9.9.9.9\n"));

// 8. CRLF сохраняется
string conf5 = "[Peer]\r\nEndpoint = 1.2.3.4:1111\r\n";
var r7 = EndpointRewriter.ReplaceHost(conf5, "9.9.9.9");
Check("CRLF сохраняется", r7 != null && r7.Contains("Endpoint = 9.9.9.9:1111\r\n") && !r7.Contains("9.9.9.9:1111\n\n"));

// 9. FindEndpoint
var parts = EndpointRewriter.FindEndpoint(conf1);
Check("FindEndpoint host/port", parts.HasValue && parts.Value.Host == "203.0.113.5" && parts.Value.Port == ":51820");
Check("FindEndpoint: нет Endpoint → null", EndpointRewriter.FindEndpoint("[Interface]\nPrivateKey = A\n") is null);

// 10. Валидация
Check("Валидация IPv4", EndpointRewriter.IsValidHost("203.0.113.7", out _));
Check("Валидация IPv6", EndpointRewriter.IsValidHost("2001:db8::1", out _));
Check("Валидация домена", EndpointRewriter.IsValidHost("vpn.example.com", out _));
Check("Валидация мусора", !EndpointRewriter.IsValidHost("!!! not a host !!!", out _));
Check("Валидация пустого", !EndpointRewriter.IsValidHost("", out _));

// 11. ApplyToFile: .bak, кодировка UTF-8 BOM, реальная запись
string dir = Path.Combine(Path.GetTempPath(), "endpoint-renamer-tests");
Directory.CreateDirectory(dir);
string f1 = Path.Combine(dir, "wg-utf8bom.conf");
File.WriteAllText(f1, conf1, new System.Text.UTF8Encoding(true));
bool changed = EndpointRewriter.ApplyToFile(f1, "10.10.10.10", out string msg);
var bytes1 = File.ReadAllBytes(f1);
bool hasBom = bytes1.Length >= 3 && bytes1[0] == 0xEF && bytes1[1] == 0xBB && bytes1[2] == 0xBF;
Check("ApplyToFile: файл изменён", changed);
Check("ApplyToFile: создан .bak с оригиналом",
    File.Exists(f1 + ".bak") && File.ReadAllText(f1 + ".bak").Contains("203.0.113.5"));
Check("ApplyToFile: UTF-8 BOM сохранён", hasBom);
Check("ApplyToFile: новый host записан",
    EndpointRewriter.ReadFilePreservingEncoding(f1, out _).Contains("Endpoint = 10.10.10.10:51820"));

// 12. UTF-16 LE конфиг
string f2 = Path.Combine(dir, "wg-utf16.conf");
File.WriteAllText(f2, conf1, System.Text.Encoding.Unicode);
EndpointRewriter.ApplyToFile(f2, "10.20.30.40", out _);
var bytes2 = File.ReadAllBytes(f2);
Check("ApplyToFile: UTF-16 LE сохранён",
    bytes2.Length >= 2 && bytes2[0] == 0xFF && bytes2[1] == 0xFE
    && EndpointRewriter.ReadFilePreservingEncoding(f2, out _).Contains("Endpoint = 10.20.30.40:51820"));

// 13. ApplyToFile на файле без Endpoint
string f3 = Path.Combine(dir, "no-endpoint.conf");
File.WriteAllText(f3, "[Interface]\nPrivateKey = A\n");
bool changed3 = EndpointRewriter.ApplyToFile(f3, "1.1.1.1", out string msg3);
Check("ApplyToFile: без Endpoint → false", !changed3 && msg3.Length > 0);

Console.WriteLine();
Console.WriteLine(failed == 0 ? "ВСЕ ТЕСТЫ ПРОШЛИ" : $"ПРОВАЛЕНО: {failed}");
return failed;
