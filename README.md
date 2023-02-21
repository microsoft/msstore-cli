# Microsoft Store Developer Command Line Interface (CLI)

[![CI](https://github.com/microsoft/msstore-cli/actions/workflows/build.yml/badge.svg)](https://github.com/microsoft/msstore-cli/actions/workflows/build.yml)

## About
The Microsoft Store Developer Command Line Interface is a cross-platform (Windows, MacOS, Linux) CLI that helps developers access the Microsoft Store APIs, for both managed (MSIX), as well as unmanaged (MSI/EXE) applications. It helps developers by creating required online resources (credentials), as well as later setting up their application projects (UWPs, Win32s, Flutter, PWAs, Electron, React-Native, as well as many other types of Windows applications) to be ready to ship to the Microsoft Store, going from the initial steps of configuring the application's manifest, as well as the actual publishing of an MSIX or MSI/EXE.

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
