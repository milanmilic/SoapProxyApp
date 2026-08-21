# SOAP Proxy App

A Windows WPF application designed for intercepting, inspecting, and replaying SOAP and JSON network traffic.

## Features
- Intercept HTTP/HTTPS request and response packets (Explicit or Global System Proxy)
- **Replay / Resend Requests** to the server with modified headers and body, on the fly!
- View raw Headers, Raw Body, Formatted XML, Formatted JSON, HTML, and Images
- Automatic conversion from SOAP XML to formatted JSON for easier reading
- Collapsible syntax-highlighted code editors for XML and JSON (AvalonEdit)
- Compare two requests or responses side-by-side using external diff tools (VS Code / WinMerge)
- Automatic extraction and display of SOAPAction headers
- Automatic resolution of IIS Application Pool names for w3wp processes
- Visual status indicators (Green/Gray/Red) for HTTP response codes
- Real-time list filtering by URL, Method, Status, or App Name
- Export individual requests and responses to text, XML, or JSON files
- Clear all captured sessions with a single click

## Installation & Usage (For Users)
This application is **Portable** and does not require an installation wizard or a .NET runtime installed on your machine.

1. Download the SoapProxyApp.exe file from the *Releases* section.
2. **Important:** Because this is a downloaded executable, Windows SmartScreen may block it. Before running, Right-Click the .exe file -> **Properties** -> Check the **Unblock** box at the bottom -> Click **OK**.
3. **Run as Administrator:** For the application to resolve IIS AppPool names (and to automatically install the proxy certificate), you must right-click and select **Run as Administrator**.
4. Click the **Start Proxy** button. (Optionally check **System Proxy** to intercept ALL computer traffic like Fiddler).
5. Click the **Install Cert** button. 
6. Start making requests from your WCF or Web application (traffic will automatically route through the proxy and appear in the list).

## What's New (Changelog)

### v1.9.1
- **Granular Save/Load:** You can now save individual or multi-selected sessions via the right-click context menu. Loading sessions now intelligently prompts whether to append to the existing list or clear it first.

### v1.9.0
- **Save & Load Sessions:** Save captured transactions to a `.sps` file and load them later for offline inspection.
- **Delete Transactions:** Select specific transactions in the list and remove them via the context menu or by pressing the `Delete` key.

### v1.8.0
- **Replay / Resend Request:** Right-click any captured session to open it in a Replay Window. Edit the URL, Method, Headers (including auth tokens), and Body, then send it again. The replayed request is automatically routed through the proxy so you can inspect the response!

### v1.7.0
- **HTML and Image Tabs:** Added dedicated tabs for previewing raw HTML and rendering captured Images directly in the UI.
- **Smart Auto-Tab Selection:** Clicking a transaction now automatically switches to the most logical tab based on the Content-Type header (e.g. automatically opening the Image tab for images, or Formatted JSON for JSON responses).

### v1.6.0
- **System Proxy Toggle:** Added a checkbox to instantly register the app as the Windows System Proxy, intercepting ALL traffic from browsers and other apps.
- **Real-time Filter:** Added a live search box above the session list to quickly filter out noise by URL, App Name, or Method.

### v1.5.1
- **Dynamic Versioning:** App now automatically detects its version from assembly metadata.
- **Auto-Updater:** The application checks GitHub on startup for new releases and prompts the user with a direct download link.
- **Admin Privileges:** Embedded an pp.manifest to automatically request Administrator privileges, guaranteeing the ability to read WMI process command lines.

### v1.4.0
- **Diff Tool Integration:** Select two sessions (Ctrl+Click), right-click, and compare them side-by-side using VS Code or WinMerge.
- **IIS AppPool Parsing:** Client processes named w3wp are now inspected via WMI to display their actual Application Pool name.
- **SOAPAction Extraction:** The SOAPAction header is now visually extracted and displayed prominently above the Request tabs.

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
