using System.IO;
using System.Windows;

namespace WireShift;

public partial class App : Application
{
    private void App_Startup(object sender, StartupEventArgs e)
    {
        Loc.Instance.Load();
        var window = new MainWindow();

        // WireShift.exe [папка] [новый-host] — удобно для «Открыть с помощью»
        if (e.Args.Length >= 1 && Directory.Exists(e.Args[0]))
        {
            window.LoadFolder(e.Args[0]);
            if (e.Args.Length >= 2)
                window.PrefillHost(e.Args[1]);
        }

        window.Show();
    }
}
