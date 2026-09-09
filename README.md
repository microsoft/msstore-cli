# Microsoft Store Developer Command Line Interface (CLI)

[![CI](https://github.com/microsoft/msstore-cli/actions/workflows/build.yml/badge.svg)](https://github.com/microsoft/msstore-cli/actions/workflows/build.yml)

## About
The Microsoft Store Developer Command Line Interface is a cross-platform (Windows, MacOS, Linux) CLI that helps developers access the Microsoft Store APIs, for both managed (MSIX), as well as unmanaged (MSI/EXE) applications. It helps developers by creating required online resources (credentials), as well as later setting up their application projects (UWPs, Win32s, Flutter, PWAs, Electron, React-Native, as well as many other types of Windows applications) to be ready to ship to the Microsoft Store, going from the initial steps of configuring the application's manifest, as well as the actual publishing of an MSIX or MSI/EXE.

## Helpful links
* [Documentation](https://aka.ms/msstoredevcli/docs) - Microsoft's official documentation on regards to available commands, installation steps, how to properly setup CI/CD environments, and general guidance.

## Standard output vs. standard error

By default the CLI splits its output as follows:

* **stdout** carries the command's result — machine-readable payloads such as the JSON emitted by `submission get`, `apps get` and `submission rollout get`, the package path printed by `package`, and `--help` text. This keeps `msstore submission get ... | ConvertFrom-Json` and `$(msstore package ...)` reliable.
* **stderr** carries everything else meant for a human — progress, status, success messages, tables, prompts and verbose logging.

`--output-stream stdout` deliberately breaks that separation: it moves the human-readable half onto stdout, where it is interleaved with any payload.

Two things sit outside the option's scope on purpose, matching the behavior of other CLIs:

* Machine-readable payloads are always written to stdout, so they are never affected by the option.
* `--help` is always written to stdout, so that `msstore --help | more` works, and command line parse errors are always written to stderr, because they accompany a non-zero exit code.

### Azure DevOps

Azure DevOps reports every stderr line as `##[error]`, even when the command succeeded and even when the task sets `failOnStderr: false`. A successful `msstore publish` therefore shows up as a failed or partially failed stage.

To avoid this, move the human-readable output to stdout:

```yaml
- script: msstore publish ./MyApp --output-stream stdout
  displayName: Publish to the Microsoft Store
```

Or set it once for a whole job, so that every `msstore` call picks it up:

```yaml
variables:
  MSSTORE_OUTPUT_STREAM: stdout
```

> [!IMPORTANT]
> Machine-readable payloads always go to stdout. When `MSSTORE_OUTPUT_STREAM` is set for a whole job, the human-readable output is interleaved with the payload, which breaks capturing it. Pass `--output-stream stderr` on those specific calls to opt back out — the option always overrides the environment variable:
>
> ```yaml
> variables:
>   MSSTORE_OUTPUT_STREAM: stdout
>
> steps:
> - script: msstore publish ./MyApp                                   # human-readable output on stdout
> - script: msstore submission get $(AppId) --output-stream stderr    # clean JSON on stdout
> ```

### GitHub Actions

No change is needed. GitHub Actions fails a step based on its exit code alone and never turns stderr into an error annotation, so the default is already correct.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.

## Data/Telemetry

The `msstore.exe` client is instrumented to collect usage and diagnostic (error) data and sends it to Microsoft to help improve the product.

If you build the client yourself the instrumentation will not be enabled and no data will be sent to Microsoft.

See the [privacy statement](privacy.md) for more details.

### Telemetry Configuration

Telemetry collection is on by default. To opt out, please run `msstore settings --enableTelemetry false` to turn it off.
