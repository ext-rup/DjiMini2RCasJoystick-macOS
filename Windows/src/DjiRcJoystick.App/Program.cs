namespace DjiRcJoystick.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        Options options;
        try
        {
            options = Options.Parse(arguments);
        }
        catch (ArgumentException exception)
        {
            ConsoleWindow.Attach();
            Console.Error.WriteLine($"Error: {exception.Message}");
            Console.Error.WriteLine(Options.Usage);
            return 1;
        }

        if (options.ShowHelp || options.ListPorts || options.Monitor)
        {
            ConsoleWindow.Attach();
            try
            {
                if (options.ShowHelp) Console.WriteLine(Options.Usage);
                if (options.ListPorts) return DiagnosticCommands.ListPorts();
                if (options.Monitor) return DiagnosticCommands.Monitor(options);
                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Error: {exception.Message}");
                return 1;
            }
        }

        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext(options);
        Application.Run(context);
        return 0;
    }
}
