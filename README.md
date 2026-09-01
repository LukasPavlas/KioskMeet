# KioskMeet – lokální kiosk aplikace pro Google Meet (4Camping)

Nahrazuje předchozí řešení (Chrome `--kiosk` + `meet.php`). Appka běží jako
samostatný proces, hlídá schůzku nativně a po jejím konci se sama vrátí zpět
na úvodní obrazovku.

## Co budete potřebovat

1. **Windows 10/11** na kiosk PC.
2. **WebView2 Runtime** – na většině aktuálních Windows 10/11 je už
   předinstalovaný. Pokud ne, appka to sama nahlásí chybou při startu;
   runtime lze doinstalovat zde:
   https://developer.microsoft.com/microsoft-edge/webview2/
3. **.NET 8 SDK** na vývojovém/sestavovacím počítači (na kiosk PC stačí
   výsledná zkompilovaná appka, SDK tam nemusí být, pokud publikujete
   jako self-contained – viz níže).

## Sestavení (na počítači, kde budete appku vyvíjet/kompilovat)

1. Nainstalujte [.NET 8 SDK](https://dotnet.microsoft.com/download).
2. Otevřete PowerShell / příkazovou řádku ve složce s projektem
   (obsahuje `KioskMeet.csproj`).
3. Obnovte balíčky a sestavte:

   ```powershell
   dotnet restore
   dotnet build -c Release
   ```

4. Vyzkoušejte appku lokálně:

   ```powershell
   dotnet run
   ```

   (Appka se spustí přes celou obrazovku – pro ukončení během vývoje
   stiskněte `Ctrl+Alt+X`.)

## Vytvoření distribuovatelné verze pro kiosk PC

Pro nasazení doporučuji "self-contained" publikaci, aby na kiosk PC nebylo
potřeba instalovat .NET runtime zvlášť:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish
```

Výsledek najdete ve složce `publish\` – zkopírujte **celou tuto složku**
(včetně `wwwroot`) na kiosk PC, např. do `C:\KioskMeet\`.

> Poznámka: `wwwroot` se musí nacházet ve stejné složce jako `KioskMeet.exe`,
> jinak appka nenajde `index.html`.

## Automatické spuštění při startu Windows

Doporučuji Naplánované úlohy (Task Scheduler), protože umožňují spustit
appku "Při přihlášení" i s vyššími oprávněními a případně automatický
restart appky, pokud by spadla:

1. Otevřete **Naplánované úlohy** → **Vytvořit úlohu…**
2. **Obecné**: název `KioskMeet`, zaškrtnout „Spustit bez ohledu na to,
   zda je uživatel přihlášen" nebo „Spustit pouze pro přihlášeného
   uživatele" (podle toho, jak máte kiosk účet nastavený).
3. **Triggery** → Nový → *Při přihlášení* (At log on).
4. **Akce** → Nová → Spustit program: `C:\KioskMeet\KioskMeet.exe`
5. **Nastavení** → zaškrtnout „Pokud úloha selže, restartovat každou…"
   (např. 1 minutu) pro odolnost proti pádu appky.

Alternativně stačí zástupce na `KioskMeet.exe` vložit do složky
`shell:startup` daného uživatelského účtu.

## Jak appka funguje

- **MainWindow** – fullscreen bezokrajové okno, načte `wwwroot/index.html`
  (stejný vizuál jako dřívější `meet.php`). Formulář po odeslání pošle kód
  appce přes `window.chrome.webview.postMessage()`.
- **MeetWindow** – appka po přijetí kódu otevře druhé, samostatné okno
  a naviguje na `https://meet.google.com/<kod>`. Appka sleduje
  `CoreWebView2.SourceChanged` – jakmile URL opustí doménu
  `meet.google.com` (typicky přesměrování na Google Workspace po skončení
  hovoru), okno se **automaticky zavře**.
- Pro jistotu je v okně se schůzkou i viditelné tlačítko
  „✕ Ukončit a vrátit se" pro ruční návrat, kdyby Google nepřesměroval
  ihned.
- Po zavření Meet okna appka pošle zprávu `"ended"` zpět do hlavní
  stránky, která vyčistí pole a vrátí fokus – uživatel může rovnou zadat
  další kód.
- `Ctrl+Alt+X` ukončí celou appku (kiosk mód). `Alt+F4` je zablokované.

## Úprava povolených domén

Pokud by appka zavírala okno moc brzy (např. kvůli přihlašovací obrazovce
Google), lze doplnit povolené domény v `MeetWindow.xaml.cs`,
v poli `AllowedHosts`.

## Nové funkce (poslední schůzky, návod, animace)

- **Poslední schůzky** – posledních 10 zadaných kódů se ukládá lokálně
  přes `localStorage` v rámci profilu WebView2 (persistuje mezi restarty
  appky, dokud zůstává stejná složka appky/profilu na disku). Kliknutím
  na kód v seznamu se rovnou znovu připojíte.
- **Boční panel s návodem** – vysvětluje uživateli krok za krokem, jak
  kiosk používat, bez nutnosti cokoliv nastavovat.
- **Animované pozadí** – plující mraky (CSS `@keyframes`) a jemně
  pulzující slunce; běží čistě na CSS, takže zatížení CPU/GPU je
  minimální i při celodenním provozu.

Vše je řešeno čistě v `wwwroot/index.html` (žádné změny v C# kódu nejsou
potřeba pro tyto funkce).

## Podpora MS Teams a Zoom

Kiosk nyní podporuje 3 služby přes záložky ve formuláři: **Google Meet**,
**MS Teams** a **Zoom**.

- **Google Meet** – zadává se krátký kód (`abc-defg-hjk`), appka z něj
  sestaví URL. Meet nemá nativní desktop appku, běží vždy ve vestavěném
  prohlížeči (WebView2).
- **MS Teams / Zoom** – zadává se celý odkaz zkopírovaný ze schůzkové
  pozvánky (u Zoomu jde zadat i jen číslo schůzky).

### Jak funguje nativní appka

Když stránka Teams/Zoom chce přesměrovat na svůj vlastní protokol
(`msteams:`, `zoommtg:`), appka to zachytí (`NavigationStarting` v
`MeetWindow.xaml.cs`) a předá řetězec Windows shellu
(`Process.Start(..., UseShellExecute = true)`):

- **Appka je nainstalovaná** → Windows ji spustí jako běžnou desktopovou
  appku, appka pak hlídá proces (`ms-teams`/`Teams`/`Zoom`) a po jeho
  ukončení se vrátí na kiosk.
- **Appka není nainstalovaná** → `Process.Start` selže, appka to potichu
  zachytí a uživatel zůstává na webové stránce, kde si Teams/Zoom sami
  nabídnou pokračování v prohlížeči (Meet-like chování se sledováním
  URL, viz `SourceChanged`).

### Známé omezení – Zoom v systémové liště

Desktop klient Zoomu po instalaci často běží na pozadí (v systémové
liště) i mimo aktivní hovor. Pokud je tak nastavený, appka může chybně
vyhodnotit "proces stále běží" i po skončení schůzky, a k automatickému
návratu nedojde. Pro tyto případy zůstává vždy k dispozici ruční
tlačítko **„✕ Ukončit a vrátit se“** přímo v okně schůzky.

### Úprava povolených domén / názvů procesů

V `MeetWindow.xaml.cs` lze upravit:
- `AllowedHostsByService` – domény, při jejichž opuštění appka usoudí,
  že schůzka skončila (pro webovou variantu).
- `NativeProcessNamesByService` – názvy procesů, podle kterých appka
  pozná běžící nativní appku.

## Kontrola periferií (kamera + Jabra SPEAK 810)

V levém dolním rohu kiosk okna je malý stavový panel se dvěma řádky:

- **Kamera** – zjišťuje se přes WMI (`Win32_PnPEntity`, třída `Camera`/
  `Image`). Zelená tečka = nalezena a hlásí stav "OK", oranžová =
  nalezena, ale s problémem (např. chybí ovladač), červená = nenalezena.
- **Jabra 810** – zjišťuje se přes `NAudio` (enumerace audio zařízení
  podle názvu obsahujícího „Jabra“). Appka navíc **automaticky nastaví
  Jabru jako výchozí zařízení** (mikrofon i reproduktor, role Console/
  Multimedia/Communications), pokud jím právě není.

Kontrola běží při startu appky a pak každých 10 sekund, takže se stav
sám opraví i po přehození USB kabelu apod.

### Jak funguje automatické nastavení výchozího zařízení

Windows oficiálně nevystavuje veřejné API pro programové přepnutí
výchozího audio zařízení. Appka proto používá stejné nedokumentované
COM rozhraní (`IPolicyConfig`) jako běžné nástroje pro přepínání
výchozího zařízení (např. EarTrumpet) – viz `AudioDeviceHelper.cs`.
Je to spolehlivé na Windows 10/11, ale jde o nedokumentované API, takže
pokud by se na vašem konkrétním buildu Windows nechovalo spolehlivě,
appka to bezpečně odchytí (try/catch) a stavový panel ukáže oranžovou
tečku „připojena, není výchozí" – v tom případě je potřeba nastavit
Jabru jako výchozí ručně přes **Nastavení Windows → Zvuk**.

### Povolení kamery/mikrofonu ve schůzce

Okno se schůzkou (`MeetWindow`) má vlastní `PermissionRequested`
handler, který **automaticky povolí přístup ke kameře a mikrofonu**
pro Meet/Teams/Zoom (bez vyskakovacího dialogu prohlížeče – v kiosk
režimu by na něj stejně nebyl nikdo, kdo by klikl). Ostatní oprávnění
(notifikace, poloha apod.) appka rovnou zamítá.

Pokud by přesto Meet/Teams/Zoom hlásily, že kameru/mikrofon nevidí,
zkontrolujte:
1. Stavový panel v rohu kiosku (kamera/Jabra musí svítit zeleně).
2. Že Jabra 810 není v ovládacích panelech Windows zakázaná
   (Nastavení → Zvuk → Zakázaná zařízení).
3. Že žádná jiná appka (např. druhá instance Teams) kameru/mikrofon
   nedrží zamčené pro sebe.

## Ladění / testování bez appky

`wwwroot/index.html` funguje i v běžném prohlížeči (otevřete přímo
soubor) – v takovém případě tlačítko „Připojit se" otevře Meet v novém
tabu/okně přes `window.open()` jako záložní chování pro testování mimo
appku.

## Testování připojení k Teams a Zoom

Appka je nativní WPF/WebView2 (Windows-only), takže ji nelze spustit
nebo automaticky otestovat mimo Windows - tady je návod, jak si to
ověřit přímo na kiosk PC.

### Zoom - má oficiální testovací schůzku

Appka obsahuje tlačítko **„🎥 Otestovat zvuk a obraz"** (viditelné na
záložce Zoom), které otevře oficiální Zoom testovací schůzku
[zoom.us/test](https://zoom.us/test) - tam si během pár vteřin ověříte
mikrofon, reproduktor i kameru přesně stejnou cestou (stejné okno,
stejná logika automatického povolení kamery/mikrofonu i automatického
návratu), jakou appka používá pro reálné schůzky.

### MS Teams - bez veřejné testovací schůzky

Microsoft bohužel oficiální veřejnou testovací schůzku (obdobu
`zoom.us/test`) nenabízí. Pro otestování doporučuji:
1. V libovolném Teams účtu (i free) vytvořit rychlou schůzku
   „Sejít se nyní" / „Meet now" a odkaz vložit do kiosku (přes záložku
   MS Teams, nebo přes chytré vložení celé pozvánky).
2. Otestovat i cestu přes **ID schůzky** (bez odkazu) - appka otevře
   oficiální stránku `microsoft.com/microsoft-teams/join-a-meeting`
   a pokusí se pole vyplnit automaticky (viz níže).

### Manuální kontrolní seznam po nasazení na kiosk PC

- [ ] Google Meet: zadání kódu → otevře se okno, kamera/mikrofon bez
      vyskakovacího dialogu, po odchodu ze schůzky se okno samo zavře.
- [ ] Zoom: „Otestovat zvuk a obraz" → funguje mikrofon/kamera/reproduktor.
- [ ] Zoom: zadání ID + hesla → připojí se do reálné schůzky.
- [ ] Teams: vložení celého odkazu → připojí se.
- [ ] Teams: zadání jen ID + hesla → appka otevře stránku a zkusí pole
      vyplnit sama; pokud ne, ID/heslo jsou vidět v panelu a zkopírované
      ve schránce (Ctrl+V).
- [ ] Chytré vložení: zkopírujte celý text pozvánky (Ctrl+A, Ctrl+C z
      e-mailu) a vložte do pole - appka by měla sama poznat službu a
      vytáhnout jen odkaz.
- [ ] Stavový panel vlevo dole: kamera i Jabra svítí zeleně.
- [ ] Pokud je nainstalovaný nativní Teams/Zoom klient, appka se ho
      pokusí spustit místo webové verze.

## Zjednodušené zadávání (bez nutnosti celé URL)

- **Google Meet** – vždy jen krátký kód (`abc-defg-hjk`).
- **MS Teams / Zoom** – stačí **číslo schůzky** (a volitelně heslo do
  samostatného pole) - není potřeba lepit celý dlouhý odkaz.
- **Chytré vložení** – do stejného pole jde vložit i **celý text
  pozvánky** (celý zkopírovaný e-mail). Appka v něm regulárním výrazem
  najde odkaz na Meet/Teams/Zoom, automaticky přepne na správnou
  záložku a vyplní jen ten odkaz.

### Jak funguje připojení jen podle ID u Teams

Protože Microsoft (na rozdíl od Zoomu) nepodporuje sestavení odkazu ze
samotného ID přes parametry v URL, appka místo toho:
1. Otevře oficiální stránku Microsoftu pro připojení podle ID.
2. Zkusí pole **ID schůzky** / **Heslo** vyplnit sama (hledá je podle
   placeholderu, ne podle pevného ID prvku - odolnější vůči drobným
   změnám stránky, ale ne stoprocentně garantované, protože jde o
   veřejnou stránku mimo naši kontrolu).
3. Pro jistotu zároveň zkopíruje ID/heslo do schránky a zobrazí je
   v panelu v rohu okna schůzky - když se automatické vyplnění
   nepovede, stačí kliknout do pole a stisknout **Ctrl+V**.

## Ovládací lišta (Domů / Restart / Ukončit) a vlastní stránky služeb

### Vlastní stránka pro každou službu

Přepínání služby je teď fixní panel **vlevo nahoře** (mimo hlavní kartu).
Každá služba má vlastní vizuální "stránku" - barevnou hero hlavičku se
svým logem/ikonou a názvem (zelená pro Meet, fialová pro Teams, modrá
pro Zoom), zbytek formuláře (pole, tlačítko, poslední schůzky, nápověda)
zůstává sdílený, aby appka nebyla zbytečně složitá na údržbu.

### Ovládací lišta vpravo nahoře

Tři tlačítka **🏠 Domů / 🔄 Restart / ⏻ Ukončit** běží jako samostatné
vždy-navrch okno (`ControlBarWindow`), nezávislé na hlavním kiosk okně
i okně schůzky - proto zůstávají viditelná a klikatelná **i během
probíhající schůzky** (nad WebView2 oknem schůzky i nad nativní appkou
Teams/Zoom).

- **🏠 Domů** – bez potvrzení vrátí hlavní kiosk obrazovku do popředí,
  aniž by ukončil právě probíhající schůzku (ta běží dál na pozadí).
- **🔄 Restart** – po potvrzení restartuje celou appku (spustí novou
  instanci a tu současnou ukončí). Určeno pro zotavení z chybového
  stavu (zaseklé WebView2, zůstalý proces apod.).
- **⏻ Ukončit** – po potvrzení (s upozorněním, že ukončení kiosk módu
  se nedoporučuje) appku úplně vypne. Stejné potvrzení se teď zobrazí
  i při použití skryté klávesové zkratky `Ctrl+Alt+X`.

### Jak zůstává lišta navrch i nad oknem schůzky

`MeetWindow` je taky `Topmost="True"`, takže mezi dvěma topmost okny by
Windows normálně upřednostnilo to naposledy aktivované. `ControlBarWindow`
proto po otevření schůzky (a pak každé 2 sekundy jako pojistka) krátce
vypne a zapne `Topmost`, čímž se spolehlivě dostane zpět navrch. Tuto
logiku najdete v `ControlBarWindow.ReassertTopmost()`.

## Zástupci na ploše (Chrome Host + spuštění kiosku)

Ve složce `shortcuts/` je `Create-DesktopShortcuts.vbs`, který na ploše
vytvoří dva zástupce:

- **Chrome (host)** – spustí Chrome v režimu Host (`--guest`) - čistý
  profil bez přihlášení, historie a uložených hesel. Hodí se, když
  chce někdo na tom samém PC rychle prohlížet web bez zásahu do
  kiosk účtu.
- **Spustit kiosk** – spustí `KioskMeet.exe`.

### Použití

1. Otevři `shortcuts/Create-DesktopShortcuts.vbs` v textovém editoru a
   uprav proměnnou `kioskExePath` na skutečnou cestu, kam jsi appku
   nainstaloval (např. `C:\KioskMeet\KioskMeet.exe`).
2. Poklepej na soubor - zástupci se vytvoří na ploše aktuálního
   uživatele.
3. Skript se zeptá, jestli chceš zástupce vytvořit i pro **všechny
   uživatele počítače** (sdílená plocha) - to ale vyžaduje spustit
   skript jako Správce (pravé tlačítko na soubor → *Spustit jako
   správce* nefunguje přímo na .vbs, proto v takovém případě spusť
   z příkazové řádky spuštěné jako Správce: `cscript
   Create-DesktopShortcuts.vbs`).

Cesta k Chromu se hledá automaticky (`Program Files` i `Program Files
(x86)`); pokud by na cílovém PC byla jinde, uprav proměnné
`chromePath` / `chromePathX86` na začátku skriptu.
