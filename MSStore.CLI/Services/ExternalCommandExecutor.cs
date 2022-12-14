// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal class ExternalCommandExecutor : IExternalCommandExecutor
    {
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
                    }
                };
                cmd.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine(e.Data);
                        stdErr.AppendLine(e.Data);
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
    }
}
