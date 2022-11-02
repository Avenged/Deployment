using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Security;
using Spectre.Console;
using System.Reflection;
using System.Resources;
using System.Text;

internal class Program
{
    private const string DefaultPrompt = "[devadmin@cloudformsadmin-sgui-vm-316421 sgui]$";
    private static bool logeadoGit;

    private static SshClient SshClient { get; set; } = null!;

    private static readonly IEnumerable<string> projects = new List<string>()
    {
        "sgui-app-container",
        "sgui-api-container",
        "sgui-hf-container"
    };

    private static bool IsHangFireSelected()
    {
        return selectedProjects.Contains("sgui-hf-container");
    }

    private static bool IsApiSelected()
    {
        return selectedProjects.Contains("sgui-api-container");
    }

    private static bool IsAppSelected()
    {
        return selectedProjects.Contains("sgui-app-container");
    }

    private static List<string> selectedProjects = new();

    private static void Main(string[] args)
    {
        var host = "10.9.11.103";
        var username = "devadmin";
        var password = "sgui2022!";

        PasswordAuthenticationMethod method = new(username, password);
        ConnectionInfo connectionInfo = new(host, username, method);

        try
        {
            selectedProjects = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Selecciona los proyectos a deployar")
                    .Required() // Not required to have a favorite fruit
                    .PageSize(10)
                    .InstructionsText(
                        "[grey](Press [blue]<space>[/] to toggle a project, " +
                        "[green]<enter>[/] to accept)[/]")
                    .AddChoices(projects));

            SshClient = new(connectionInfo);

            AnsiConsole.Status()
                .Start("Actualizando repositorio ASI local...", ctx =>
                {
                    try
                    {
                        {
                            using System.Diagnostics.Process process = new();
                            System.Diagnostics.ProcessStartInfo startInfo = new()
                            {
                                FileName = "cmd.exe",
                                Arguments = @"/c robocopy ..\SGUI\ .\source\ -e -Xd .vs .git bin obj",
                            };

                            process.StartInfo = startInfo;
                            process.Start();
                            process.WaitForExit();
                        }

                        {
                            using System.Diagnostics.Process process = new();
                            System.Diagnostics.ProcessStartInfo startInfo = new()
                            {
                                FileName = "cmd.exe",
                                Arguments = @"/c git status",
                            };

                            process.StartInfo = startInfo;
                            process.Start();
                            process.WaitForExit();
                        }

                        {
                            using System.Diagnostics.Process process = new();
                            System.Diagnostics.ProcessStartInfo startInfo = new()
                            {
                                FileName = "cmd.exe",
                                Arguments = @"/c git add .",
                            };

                            process.StartInfo = startInfo;
                            process.Start();
                            process.WaitForExit();
                        }

                        {
                            using System.Diagnostics.Process process = new();
                            System.Diagnostics.ProcessStartInfo startInfo = new()
                            {
                                FileName = "cmd.exe",
                                Arguments = $@"/c git commit -a -m ""deploy {DateTime.Now:yyyyMMdd mmss}""",
                            };

                            Console.WriteLine(startInfo.Arguments);
                            process.StartInfo = startInfo;
                            process.Start();
                            process.WaitForExit();
                        }

                        {
                            using System.Diagnostics.Process process = new();
                            System.Diagnostics.ProcessStartInfo startInfo = new()
                            {
                                FileName = "cmd.exe",
                                Arguments = $@"/c git push origin master",
                            };

                            Console.Clear();
                            process.StartInfo = startInfo;
                            process.Start();
                            process.WaitForExit();
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                        Environment.Exit(-1);
                    }
                });

            AnsiConsole.Status()
                .Start("Conectando a 10.9.11.103...", ctx =>
                {
                    try
                    {
                        SshClient.Connect();
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                        Environment.Exit(-1);
                    }
                });

            ShellStream shellStreamSsh = SshClient.CreateShellStream("vt-100", 160, 120, 800, 600, 65536);
            shellStreamSsh.DataReceived += WaitFor_GitPull_UsernamePrompt;

            AnsiConsole.Status()
                .Start("Actualizando repositorio local...", ctx =>
                {
                    try
                    {
                        shellStreamSsh.WriteLine("export TERM=linux");
                        shellStreamSsh.WriteLine("cd sgui");
                        shellStreamSsh.WriteLine("git pull origin master");

                        while (!logeadoGit)
                        {
                            Thread.Sleep(300);
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                        Environment.Exit(-1);
                    }
                });

            Console.Clear();

            Task.Run(() =>
            {
                shellStreamSsh.Dispose();
                ExecuteNextCommands();
            });
             
            while (true)
            {
                string? command = Console.ReadLine();

                //if (command is null) continue;

                //shellStreamSsh.WriteLine(command);
                //shellStreamSsh.Flush();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private static void WaitFor_GitPull_UsernamePrompt(object? sender, ShellDataEventArgs e)
    {
        if (Listen(sender, out var shellStreamSsh).EndsWith("Username for 'https://repositorio-asi.buenosaires.gob.ar':"))
        {
            shellStreamSsh.DataReceived -= WaitFor_GitPull_UsernamePrompt;
            shellStreamSsh.DataReceived += WaitFor_GitPull_PasswordPrompt;
            shellStreamSsh.WriteLine("20316795286");
        }
    }

    private static void WaitFor_GitPull_PasswordPrompt(object? sender, ShellDataEventArgs e)
    {
        if (Listen(sender, out var shellStreamSsh).EndsWith("Password for 'https://20316795286@repositorio-asi.buenosaires.gob.ar':"))
        {
            shellStreamSsh.DataReceived -= WaitFor_GitPull_PasswordPrompt;
            shellStreamSsh.DataReceived += WaitFor_GitPull_FinishingPrompt;
            shellStreamSsh.WriteLine("Pato*Ñato");
        }
    }

    private static void WaitFor_GitPull_FinishingPrompt(object? sender, ShellDataEventArgs e)
    {
        if (Listen(sender, out var shellStreamSsh).EndsWith(DefaultPrompt, StringComparison.InvariantCultureIgnoreCase))
        {
            shellStreamSsh.DataReceived -= WaitFor_GitPull_FinishingPrompt;
            logeadoGit = true;
        }
    }

    private static void ExecuteNextCommands()
    {
        try
        {
            AnsiConsole.Status()
                .Start("Actualizando versión en BD...", ctx =>
                {
                    try
                    {
                        Do_ActualizarVersion();
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                        Environment.Exit(-1);
                    }
                });

            AnsiConsole.Status()
                .Start("Deteniendo contenedores...", ctx =>
                {
                    try
                    {
                        Do_PodmanStop();
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                        Environment.Exit(-1);
                    }
                });

            AnsiConsole.Status()
                .Start("Removiendo contenedores...", ctx =>
                {
                    try
                    {
                        Do_PodmanRm();
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                        Environment.Exit(-1);
                    }
                });

            AnsiConsole.Status()
                .Start("Removiendo imagenes...", ctx =>
                {
                    try
                    {
                        Do_PodmanRmi();
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                        Environment.Exit(-1);
                    }
                });

            AnsiConsole.Status()
                .Start("Construyendo contenedores...", ctx =>
                {
                    try
                    {
                        Do_PodmanBuild();
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                        Environment.Exit(-1);
                    }
                });

            AnsiConsole.Status()
                .Start("Iniciando contenedores...", ctx =>
                {
                    try
                    {
                        Do_PodmanRun();
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                        Environment.Exit(-1);
                    }
                });

            //SshClient.Disconnect();
            //SshClient.Dispose();

            Console.WriteLine("Finalizado. Presione cualquier tecla para cerrar.");
            Console.ReadKey();

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            Environment.Exit(-1);
        }
    }

    private static void PrintAndWaitCommand(SshCommand command)
    {
        var result = command.BeginExecute();

        new Thread(() =>
        {
            using var reader = new StreamReader(command.ExtendedOutputStream, Encoding.UTF8, true, 1024, true);
            while (!result.IsCompleted || !reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (line is not null)
                {
                    Console.WriteLine(line);
                }
            }
        }).Start();

        using (var reader = new StreamReader(command.OutputStream, Encoding.UTF8, true, 1024, true))
        {
            while (!result.IsCompleted || !reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (line is not null)
                {
                    Console.WriteLine(line);
                }
            }
        }

        command.EndExecute(result);
    }

    private static void Do_ActualizarVersion()
    {
        PrintAndWaitCommand(SshClient.CreateCommand("mysql -u sa -p#Admin1 -e 'UPDATE Parametros SET ValorChar=date_format(Now(), \"%Y.%m.%d.%H%i\") WHERE CodigoParametro=\"SGUI.Version\";' SGUI_Datos"));
    }

    private static void Do_PodmanStop()
    {
        if (IsAppSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman stop sgui-app-container"));

        if (IsApiSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman stop sgui-api-container"));

        if (IsHangFireSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman stop sgui-hf-container"));
    }

    private static void Do_PodmanRm()
    {
        if (IsAppSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman rm sgui-app-container"));

        if (IsApiSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman rm sgui-api-container"));

        if (IsHangFireSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman rm sgui-hf-container"));
    }

    private static void Do_PodmanRmi()
    {
        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman rmi --all"));
    }

    private static void Do_PodmanBuild()
    {
        if (IsAppSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman build -t sgui-app-image -f source/Dockerfile.app"));

        if (IsApiSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman build -t sgui-api-image -f source/Dockerfile.api"));

        if (IsHangFireSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman build -t sgui-hf-image -f source/Dockerfile.hf"));
    }

    private static void Do_PodmanRun()
    {
        if (IsAppSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman run -e ASPNETCORE_URLS=http://+:4000 --name sgui-app-container --pod sgui-pod localhost/sgui-app-image"));

        if (IsApiSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman run -dt -e ASPNETCORE_URLS=http://+:5000 --name sgui-api-container --pod sgui-pod localhost/sgui-api-image"));

        if (IsHangFireSelected())
            PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman run -dt -e ASPNETCORE_URLS=http://+:6000 --name sgui-hf-container --pod sgui-pod localhost/sgui-hf-image"));
    }

    private static string Listen(object? sender, out ShellStream shellStreamSsh)
    {
        shellStreamSsh = (ShellStream)sender!;
        string strData = shellStreamSsh.Read();
        Console.Write(strData);
        return strData.Trim();
    }
}