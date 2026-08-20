# SOAP Proxy App

A Windows WPF application designed for intercepting and inspecting SOAP network traffic.

## Features
- Intercept HTTP/HTTPS request and response packets
- View raw Headers and Body
- Automatic conversion from SOAP XML to formatted JSON for easier reading
- On-demand "Pretty Print" formatting for XML payloads
- Export individual requests and responses to text, XML, or JSON files
- Clear all captured sessions with a single click

## Installation & Usage (For Users)
This application is **Portable** and does not require an installation wizard or a .NET runtime installed on your machine.

1. Download the `SoapProxyApp.exe` file from the *Releases* section.
2. Double-click the executable to launch it.
3. Click the **Start Proxy** button.
4. Click the **Install Cert** button. (Note: Run the app as Administrator for automatic installation).
5. If running as Administrator, the certificate will be automatically installed to **Local Machine -> Trusted Root Certification Authorities**. If not, it will be saved to your Desktop for manual installation.
6. Start making requests from your WCF or Web application (traffic will automatically route through the proxy and appear in the list).

## For Developers
The application is built using C# (.NET 10) and WPF.
1. Open the solution or folder in Visual Studio.
2. Build the project (NuGet packages will be downloaded automatically via `nuget.config`).
3. Run the application.

## Dependencies
- Titanium.Web.Proxy
- Newtonsoft.Json