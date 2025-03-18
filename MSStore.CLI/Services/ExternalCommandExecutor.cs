// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace MSStore.CLI.Services
{
    internal class ExternalCommandExecutor(IAnsiConsole ansiConsole, ILogger<ExternalCommandExecutor> logger) : IExternalCommandExecutor
    {
        private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
        private ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<ExternalCommandExecutionResult> RunAsync(string command, string arguments, string workingDirectory, CancellationToken ct)
        {
            using (Process cmd = new Process())
            {
                cmd.StartInfo.WorkingDirectory = workingDirectory;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    cmd.StartInfo.FileName = "cmd.exe";
                    if (command.StartsWith("\"", StringComparison.OrdinalIgnoreCase) || command.StartsWith("(", StringComparison.OrdinalIgnoreCase))
                    {
                        cmd.StartInfo.Arguments = $"/C {command} {arguments}";
                    }
                    else
                    {
                        cmd.StartInfo.Arguments = $"/C ({command} {arguments})";
                    }
                }
                else
                {
                    cmd.StartInfo.FileName = command;
                    cmd.StartInfo.Arguments = arguments;
                }

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _ansiConsole.WriteLine($"Running: {cmd.StartInfo.FileName} {cmd.StartInfo.Arguments}");
                }

                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.RedirectStandardError = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;

                var stdOut = new StringBuilder();
                var stdErr = new StringBuilder();

                cmd.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine(e.Data);
                        stdOut.AppendLine(e.Data);

                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _ansiConsole.WriteLine(e.Data);
                        }
                    }
                };
                cmd.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine(e.Data);
                        stdErr.AppendLine(e.Data);

                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _ansiConsole.WriteLine(e.Data);
                        }
                    }
                };
                cmd.Start();

                cmd.BeginOutputReadLine();
                cmd.BeginErrorReadLine();

                cmd.StandardInput.Close();
                await cmd.WaitForExitAsync(ct);

                return new ExternalCommandExecutionResult
                {
                    ExitCode = cmd.ExitCode,
                    StdOut = stdOut.ToString(),
                    StdErr = stdErr.ToString()
                };
            }
        }

        public async Task<string> FindToolAsync(string command, CancellationToken ct)
        {
            ExternalCommandExecutionResult result;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = await RunAsync("where", command, Environment.CurrentDirectory, ct);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                result = await RunAsync("which", command, Environment.CurrentDirectory, ct);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            if (result.ExitCode != 0)
            {
                return string.Empty;
            }

            var toolPaths = result.StdOut.Trim();
            var toolPathList = toolPaths.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
            return toolPathList.Length == 0 ? string.Empty : toolPathList[0];
        }
    }
}
