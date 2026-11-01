using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace WireShift;

/// <summary>
/// Простой локализатор с переключением языка на лету (ru/en).
/// XAML привязывается к индексатору: {Binding Source={x:Static loc:Loc.Instance}, Path=[ключ]}.
/// </summary>
public class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WireShift");
    private static string SettingsFile => Path.Combine(SettingsDir, "language.txt");

    private static readonly Dictionary<string, (string Ru, string En)> Strings = new()
    {
        ["window_title"] = ("WireShift — замена Endpoint в WireGuard-конфигах",
                            "WireShift — replace Endpoint in WireGuard configs"),
        ["folder_placeholder"] = ("Папка не выбрана", "No folder selected"),
        ["folder_tooltip"] = ("Папка с .conf файлами", "Folder with .conf files"),
        ["pick_folder"] = ("Выбрать папку…", "Browse…"),
        ["pick_folder_dialog"] = ("Папка с WireGuard-конфигами", "Folder with WireGuard configs"),
        ["include_subfolders"] = ("Включая подпапки", "Include subfolders"),
        ["new_host_label"] = ("Новый IP / хост:", "New IP / host:"),
        ["new_host_tooltip"] = ("IPv4 (203.0.113.7), IPv6 (2001:db8::1) или домен (vpn.example.com)",
                                "IPv4 (203.0.113.7), IPv6 (2001:db8::1) or hostname (vpn.example.com)"),
        ["col_file"] = ("Файл", "File"),
        ["col_current_host"] = ("Текущий host", "Current host"),
        ["col_port"] = ("Порт", "Port"),
        ["col_preview"] = ("После замены", "After replacement"),
        ["col_status"] = ("Статус", "Status"),
        ["select_all"] = ("Выбрать все", "Select all"),
        ["select_none"] = ("Снять все", "Select none"),
        ["apply"] = ("Применить замену", "Apply replacement"),

        ["status_select_folder"] = ("Выберите папку с конфигами", "Select a folder with configs"),
        ["status_found"] = ("Найдено конфигов: {0}{1}. Введите новый адрес и нажмите «Применить замену».",
                            "Configs found: {0}{1}. Enter the new address and click \"Apply replacement\"."),
        ["status_not_found"] = ("Конфиги с полем Endpoint не найдены{0}",
                                "No configs with an Endpoint field found{0}"),
        ["skipped_suffix"] = (", без Endpoint: {0}", ", without Endpoint: {0}"),
        ["error_reading_folder"] = ("Ошибка чтения папки: {0}", "Error reading folder: {0}"),
        ["status_none_selected"] = ("Не выбрано ни одного конфига (галочки слева).",
                                    "No configs selected (checkboxes on the left)."),
        ["confirm_title"] = ("Подтверждение", "Confirmation"),
        ["confirm_text"] = ("Заменить host на «{0}» в {1} файл(ах)?\nПорт сохранится, рядом с каждым файлом будет создан .bak.",
                            "Replace host with \"{0}\" in {1} file(s)?\nThe port will be preserved and a .bak copy will be created next to each file."),
        ["status_done"] = ("Готово: изменено {0} из {1}{2}. Оригиналы сохранены рядом с расширением .bak",
                           "Done: {0} of {1} changed{2}. Originals are saved next to the files with the .bak extension"),
        ["failed_suffix"] = (", ошибок: {0}", ", errors: {0}"),
        ["status_replaced"] = ("✔ Заменено (создан .bak)", "✔ Replaced (.bak created)"),
        ["msg_no_endpoint"] = ("Endpoint не найден или уже совпадает", "Endpoint not found or already matches"),
        ["error_prefix"] = ("Ошибка: {0}", "Error: {0}"),

        ["validation_empty"] = ("Поле пустое", "Field is empty"),
        ["validation_bad"] = ("Не похоже на IPv4, IPv6 или доменное имя",
                              "Does not look like an IPv4, IPv6 or hostname"),
    };

    public string Language { get; private set; } = "ru";

    public string this[string key] =>
        Strings.TryGetValue(key, out var v) ? (Language == "ru" ? v.Ru : v.En) : $"#{key}";

    /// <summary>Форматированная строка: F("status_found", count, suffix).</summary>
    public string F(string key, params object?[] args) => string.Format(this[key], args);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetLanguage(string lang)
    {
        if (lang is not ("ru" or "en") || Language == lang) return;
        Language = lang;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        Save();
    }

    /// <summary>Восстанавливает выбранный язык; по умолчанию — по языку системы.</summary>
    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string lang = File.ReadAllText(SettingsFile).Trim();
                if (lang is "ru" or "en")
                {
                    Language = lang;
                    return;
                }
            }
        }
        catch { /* настройки не критичны */ }

        Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsFile, Language);
        }
        catch { /* настройки не критичны */ }
    }
}
