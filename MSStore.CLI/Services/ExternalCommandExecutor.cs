// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
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
                cmd.StartInfo.Arguments = $"/C \"{command}\"";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.RedirectStandardError = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();

                cmd.StandardInput.Close();
                await cmd.WaitForExitAsync(ct);

                var stdOut = await cmd.StandardOutput.ReadToEndAsync(ct);
                var stdErr = await cmd.StandardError.ReadToEndAsync(ct);

                return new ExternalCommandExecutionResult
                {
                    ExitCode = cmd.ExitCode,
                    StdOut = stdOut,
                    StdErr = stdErr
                };
            }
        }
    }
}
