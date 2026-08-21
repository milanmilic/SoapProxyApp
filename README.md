# SOAP Proxy App

A Windows WPF application designed for intercepting and inspecting SOAP and JSON network traffic.

## Features
- Intercept HTTP/HTTPS request and response packets
- View raw Headers, Raw Body, Formatted XML, and JSON
- Automatic conversion from SOAP XML to formatted JSON for easier reading
- Collapsible syntax-highlighted code editors for XML and JSON (AvalonEdit)
- Compare two requests or responses side-by-side using external diff tools (VS Code / WinMerge)
- Automatic extraction and display of SOAPAction headers
- Automatic resolution of IIS Application Pool names for w3wp processes
- Visual status indicators (Green/Gray/Red) for HTTP response codes
- Export individual requests and responses to text, XML, or JSON files
- Clear all captured sessions with a single click

## Installation & Usage (For Users)
This application is **Portable** and does not require an installation wizard or a .NET runtime installed on your machine.

1. Download the SoapProxyApp.exe file from the *Releases* section.
2. **Important:** Because this is a downloaded executable, Windows SmartScreen may block it. Before running, Right-Click the .exe file -> **Properties** -> Check the **Unblock** box at the bottom -> Click **OK**.
3. **Run as Administrator:** For the application to resolve IIS AppPool names (and to automatically install the proxy certificate), you must right-click and select **Run as Administrator**.
4. Click the **Start Proxy** button.
5. Click the **Install Cert** button. 
6. Start making requests from your WCF or Web application (traffic will automatically route through the proxy and appear in the list).

## What's New (Changelog)

### v1.5.1
- **Dynamic Versioning:** App now automatically detects its version from assembly metadata.
- **Auto-Updater:** The application checks GitHub on startup for new releases and prompts the user with a direct download link.
- **Admin Privileges:** Embedded an pp.manifest to automatically request Administrator privileges, guaranteeing the ability to read WMI process command lines.

### v1.4.0
- **Diff Tool Integration:** Select two sessions (Ctrl+Click), right-click, and compare them side-by-side using VS Code or WinMerge.
- **Smart Diff Filenames:** Diff temporary files are now named intuitively (e.g. StaffTravel_GetFlightCapacity_143522_Req1.xml).
- **IIS AppPool Parsing:** Client processes named w3wp are now inspected via WMI to display their actual Application Pool name.
- **SOAPAction Extraction:** The SOAPAction header is now visually extracted and displayed prominently above the Request tabs.
- **List Highlights:** Selected items in the sessions list now have a distinctive blue border and active background color in both Light and Dark themes.

### v1.3.0
- **Syntax Highlighting & Folding:** Replaced standard TextBoxes with AvalonEdit. XML and JSON tabs now feature syntax highlighting and collapsible nodes.
- **Status Backgrounds:** List items are now conditionally colored (Pale Green for 2xx, Pale Gray for 4xx, Pale Red for 5xx).
- **Accurate Content Length:** Re-engineered the content-length calculator to measure uncompressed byte arrays.
- **Auto-Scrolling:** The sessions list now automatically scrolls to the newest traffic.

## For Developers
The application is built using C# (.NET 10) and WPF.
1. Open the solution or folder in Visual Studio.
2. Build the project (NuGet packages will be downloaded automatically).
3. Run the application.

## Dependencies
- Titanium.Web.Proxy
- Newtonsoft.Json
- AvalonEdit
- System.Management
