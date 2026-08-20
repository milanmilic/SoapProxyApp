# SOAP Proxy App

Windows WPF aplikacija za presretanje SOAP mrežnog saobraćaja. 

## Mogućnosti
- Hvatanje HTTP/HTTPS request i response paketa
- Prikaz zaglavlja (Headers) i tela (Body)
- Automatska konverzija iz SOAP XML formata u JSON format za lakše čitanje
- Izvoz pojedinačnih zahteva i odgovora u tekstualne/XML/JSON fajlove
- Brisanje svih sačuvanih sesija jednim klikom

## Instalacija & Korišćenje (Za korisnike)
Ova aplikacija je **Portable** (ne zahteva instalaciju ni instaliran .NET).

1. Preuzmi fajl `SoapProxyApp.exe` iz sekcije *Releases*.
2. Dvoklikni na fajl da ga pokreneš.
3. Klikni na dugme **Start Proxy**.
4. Klikni na dugme **Export Cert**. Aplikacija će sačuvati sertifikat na Desktop.
5. Dvoklikni na sertifikat na Desktopu, klikni *Install Certificate*.
6. Obavezno izaberi **Local Machine** -> *Place all certificates in the following store* -> *Browse* -> **Trusted Root Certification Authorities**.
7. Započni slanje zahteva iz tvoje web aplikacije (ona će sada automatski rutirati saobraćaj kroz ovaj proxy).

## Za Developere
Aplikacija je kreirana koristeći C# (.NET 10) i WPF. 
1. Otvoriti folder u Visual Studio-u.
2. Build projekta (automatski će skinuti NuGet pakete iz `nuget.config`).
3. Pokrenuti aplikaciju.

## Zavisnosti
- Titanium.Web.Proxy
- Newtonsoft.Json
