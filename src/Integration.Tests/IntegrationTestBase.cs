﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Integration.Tests;
public class IntegrationTestBase : IDisposable
{
    private readonly IPythonEnvironment env;
    private readonly IHost app;

    public IntegrationTestBase()
    {
        string pythonVersionWindows = "3.12.4";
        string pythonVersionMacOS = Environment.GetEnvironmentVariable("PYTHON_VERSION") ?? "3.12";
        string pythonVersionLinux = Environment.GetEnvironmentVariable("PYTHON_VERSION") ?? "3.12";
        string venvPath = Path.Join(Environment.CurrentDirectory, "python", ".venv");


        app = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var pb = services.WithPython();
                pb.WithHome(Path.Join(Environment.CurrentDirectory, "python"));

                pb.FromSource(@"C:\Users\anthonyshaw\source\repos\cpython", "3.12")
                    .FromNuGet(pythonVersionWindows)
                  .FromMacOSInstallerLocator(pythonVersionMacOS)
                  .FromEnvironmentVariable("Python3_ROOT_DIR", pythonVersionLinux)
                  .WithVirtualEnvironment(venvPath)
                  .WithPipInstaller();

                services.AddLogging(builder => builder.AddXUnit());
            })
            .Build();

        env = app.Services.GetRequiredService<IPythonEnvironment>();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        GC.Collect();
    }

    public IPythonEnvironment Env => env;
}
