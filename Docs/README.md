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
