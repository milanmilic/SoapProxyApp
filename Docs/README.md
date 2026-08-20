# SOAP Proxy App

Windows WPF aplikacija za presretanje SOAP mrežnog saobraćaja. 

## Mogućnosti
- Hvatanje HTTP/HTTPS request i response paketa
- Prikaz zaglavlja (Headers) i tela (Body)
- Automatska konverzija iz SOAP XML formata u JSON format za lakše čitanje

## Instalacija
Aplikacija je kreirana koristeći .NET 8 (ili 9) WPF. 
1. Otvoriti `SoapProxyApp.sln` ili folder u Visual Studio-u.
2. Build projekta (automatski će skinuti NuGet pakete).
3. Pokrenuti aplikaciju.

## Korišćenje
1. Kliknuti na dugme `Start Proxy` u aplikaciji.
2. Podesiti vaš SOAP klijent da koristi proxy: `127.0.0.1:8888`.
3. Svi zahtevi će se pojaviti u listi sa leve strane. Klikom na zahtev, možete videti detalje sa desne strane, uključujući i JSON tab.

## Zavisnosti
- Titanium.Web.Proxy
- Newtonsoft.Json
## Installation & Usage

1. Download \SoapProxyApp.exe\ from the latest release.
2. Run the application (no installation required).
3. Click **Start Proxy**.
4. Click **Export Cert** and follow the instructions to install the proxy certificate to your **Local Machine -> Trusted Root Certification Authorities** store. This is required for your local WCF/Web apps to trust the proxy.
5. Your web applications will now automatically route traffic through this proxy and you can inspect SOAP/JSON payloads.
