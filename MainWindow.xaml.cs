using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WireShift;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ConfigEntry> _entries = new();
    private bool _langReady;

    public MainWindow()
    {
        InitializeComponent();
        ConfigsGrid.ItemsSource = _entries;

        LangBox.Items.Add(new ComboBoxItem { Content = "Русский", Tag = "ru" });
        LangBox.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
        LangBox.SelectedIndex = Loc.Instance.Language == "ru" ? 0 : 1;
        _langReady = true;

        StatusText.Text = Loc.Instance["status_select_folder"];
    }

    private void LangBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_langReady || LangBox.SelectedItem is not ComboBoxItem item) return;
        Loc.Instance.SetLanguage((string)item.Tag);

        // Если папка ещё не загружена — обновим стартовую подсказку сразу
        if (_entries.Count == 0 && !Directory.Exists(FolderBox.Text))
            StatusText.Text = Loc.Instance["status_select_folder"];
        NewHostBox_TextChanged(this, null!);
    }

    private void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Loc.Instance["pick_folder_dialog"] };
        if (dialog.ShowDialog(this) != true) return;

        FolderBox.Text = dialog.FolderName;
        ScanFolder(dialog.FolderName);
    }

    private void Rescan_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(FolderBox.Text))
            ScanFolder(FolderBox.Text);
    }

    /// <summary>Загружает папку извне (аргумент командной строки).</summary>
    public void LoadFolder(string folder)
    {
        FolderBox.Text = folder;
        ScanFolder(folder);
    }

    /// <summary>Подставляет адрес в поле нового host (аргумент командной строки).</summary>
    public void PrefillHost(string host) => NewHostBox.Text = host;

    private void ScanFolder(string folder)
    {
        _entries.Clear();
        var option = RecursiveBox.IsChecked == true
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*.conf", option);
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.Instance.F("error_reading_folder", ex.Message);
            return;
        }

        int skipped = 0;
        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string content;
            try
            {
                content = EndpointRewriter.ReadFilePreservingEncoding(file, out _);
            }
            catch
            {
                skipped++;
                continue;
            }

            var endpoint = EndpointRewriter.FindEndpoint(content);
            if (endpoint is null)
            {
                skipped++;
                continue;
            }

            var parts = endpoint.Value;
            _entries.Add(new ConfigEntry
            {
                FullPath = file,
                RelativePath = Path.GetRelativePath(folder, file),
                CurrentHost = parts.Host,
                Port = parts.Port,
            });
        }

        UpdatePreviews();
        string skippedSuffix = skipped > 0 ? Loc.Instance.F("skipped_suffix", skipped) : "";
        StatusText.Text = _entries.Count == 0
            ? Loc.Instance.F("status_not_found", skippedSuffix)
            : Loc.Instance.F("status_found", _entries.Count, skippedSuffix);
    }

    private void NewHostBox_TextChanged(object sender, TextChangedEventArgs e)
        => UpdatePreviews();

    private void UpdatePreviews()
    {
        string host = NewHostBox.Text.Trim();
        bool valid = EndpointRewriter.IsValidHost(host, out string errorKey);

        ValidationText.Text = string.IsNullOrEmpty(host) ? "" : (valid ? "" : Loc.Instance[errorKey]);
        ApplyButton.IsEnabled = valid && _entries.Count > 0;

        foreach (var entry in _entries)
            entry.Preview = valid
                ? EndpointRewriter.FormatHost(host) + entry.Port
                : "";
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _entries) entry.IsSelected = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _entries) entry.IsSelected = false;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        string host = NewHostBox.Text.Trim();
        var selected = _entries.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText.Text = Loc.Instance["status_none_selected"];
            return;
        }

        var confirm = MessageBox.Show(
            this,
            Loc.Instance.F("confirm_text", EndpointRewriter.FormatHost(host), selected.Count),
            Loc.Instance["confirm_title"],
            MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        int ok = 0, failed = 0;
        foreach (var entry in selected)
        {
            try
            {
                bool changed = EndpointRewriter.ApplyToFile(entry.FullPath, host, out string messageKey);
                entry.Status = changed
                    ? Loc.Instance["status_replaced"]
                    : $"— {Loc.Instance[messageKey]}";
                if (changed)
                {
                    entry.CurrentHost = EndpointRewriter.FormatHost(host);
                    ok++;
                }
            }
            catch (Exception ex)
            {
                entry.Status = Loc.Instance.F("error_prefix", ex.Message);
                failed++;
            }
        }

        string failedSuffix = failed > 0 ? Loc.Instance.F("failed_suffix", failed) : "";
        StatusText.Text = Loc.Instance.F("status_done", ok, selected.Count, failedSuffix);
    }
}
