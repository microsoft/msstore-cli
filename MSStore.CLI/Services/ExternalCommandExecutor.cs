// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal class ExternalCommandExecutor : IExternalCommandExecutor
    {
        public async Task<ExternalCommandExecutionResult> RunAsync(string command, string workingDirectory, CancellationToken ct)
        {
            using (Process cmd = new Process())
            {
                cmd.StartInfo.WorkingDirectory = workingDirectory;
                cmd.StartInfo.FileName = "cmd.exe";
                if (command.StartsWith("\"") || command.StartsWith("("))
                {
                    cmd.StartInfo.Arguments = $"/C {command}";
                }
                else
                {
                    cmd.StartInfo.Arguments = $"/C ({command})";
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
                        stdOut.Append(e.Data);
                    }
                };
                cmd.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine(e.Data);
                        stdErr.Append(e.Data);
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
