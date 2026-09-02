# KioskMeet

Windows kiosk aplikace pro připojování k online schůzkám (Google Meet,
Microsoft Teams, Zoom) na sdílených zasedačkových/recepčních počítačích.
Uživatel zadá kód nebo odkaz schůzky, appka schůzku otevře v
odděleném okně a po jejím skončení se sama vrátí zpět na úvodní
obrazovku – bez potřeby cokoliv ručně zavírat.

![Platforma](https://img.shields.io/badge/platforma-Windows%2010%2F11-blue)
![Licence](https://img.shields.io/badge/licence-MIT-green)

---

## Obsah

- [Co appka umí](#co-appka-umí)
- [Požadavky](#požadavky)
- [Rychlý start (hotový build)](#rychlý-start-hotový-build)
- [Sestavení ze zdrojového kódu](#sestavení-ze-zdrojového-kódu)
- [Nasazení na kiosk počítač](#nasazení-na-kiosk-počítač)
- [Jak appka funguje](#jak-appka-funguje)
- [Konfigurace](#konfigurace)
- [Kontrola periferií (kamera + mikrofon)](#kontrola-periferií-kamera--mikrofon)
- [Testování připojení](#testování-připojení)
- [Zástupci na ploše](#zástupci-na-ploše)
- [Vydávání nových verzí](#vydávání-nových-verzí)
- [Podpora platforem](#podpora-platforem)
- [Licence](#licence)

---

## Co appka umí

- **Tři schůzkové služby** – Google Meet, Microsoft Teams a Zoom, každá
  na vlastní barevně odlišené stránce s přepínačem vlevo nahoře.
- **Zjednodušené zadávání** – u Meetu stačí krátký kód, u Teams/Zoom jde
  zadat jen číslo schůzky (+ volitelně heslo), nebo vložit celou
  zkopírovanou pozvánku – appka si v ní sama najde odkaz.
- **Automatický návrat po skončení schůzky** – appka sleduje, kdy
  schůzka skončila (podle URL nebo běžícího procesu nativní appky), a
  sama zavře okno a vrátí uživatele na úvodní obrazovku.
- **Automatické povolení kamery/mikrofonu** – žádné vyskakovací dialogy
  prohlížeče, o které by se na kiosku neměl kdo starat.
- **Automatická kontrola a přepnutí zvukového zařízení** – appka umí
  rozpoznat konkrétní USB reproduktor/mikrofon (např. konferenční
  jednotku) a nastavit ho jako výchozí.
- **Stavový panel** – v rohu obrazovky ukazuje, jestli je kamera a
  zvukové zařízení připojené a funkční.
- **Ovládací lišta** (Domů / Restart / Ukončit) – vždy viditelná, i
  během probíhající schůzky.
- **Poslední schůzky** – rychlé opětovné připojení jedním kliknutím.
- **Podpora nativních desktopových appek** – pokud je na počítači
  nainstalovaný Teams nebo Zoom klient, kiosk se ho pokusí spustit
  místo webové verze.

## Požadavky

| | |
|---|---|
| Operační systém (kiosk PC) | Windows 10 nebo 11 |
| WebView2 Runtime | Obvykle už předinstalovaný; appka na chybějící runtime sama upozorní. [Stáhnout zde](https://developer.microsoft.com/microsoft-edge/webview2/) |
| .NET 8 SDK | Pouze na počítači, kde appku sestavujete – na kiosk PC stačí hotový build |

## Rychlý start (hotový build)

Nejjednodušší cesta – stáhnout hotový sestavený balíček ze záložky
**Releases** tohoto repozitáře, rozbalit ZIP a spustit `KioskMeet.exe`.
Nic dalšího instalovat nemusíte (build je self-contained).

## Sestavení ze zdrojového kódu

1. Nainstalujte [.NET 8 SDK](https://dotnet.microsoft.com/download).
2. V kořenové složce projektu (obsahuje `KioskMeet.csproj`) spusťte:

   ```powershell
   dotnet restore
   dotnet build -c Release
   ```

3. Pro vyzkoušení appky lokálně:

   ```powershell
   dotnet run
   ```

   Appka se spustí přes celou obrazovku. Pro ukončení během vývoje
   stiskněte `Ctrl+Alt+X`, nebo použijte tlačítko „Ukončit" v ovládací
   liště v pravém horním rohu.

## Nasazení na kiosk počítač

Pro reálné nasazení doporučujeme „self-contained" publikaci, aby na
kiosk PC nebylo potřeba instalovat .NET runtime zvlášť:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish
```

Výsledek najdete ve složce `publish\` – zkopírujte **celou tuto
složku** (včetně podsložky `wwwroot`) na kiosk PC, např. do
`C:\KioskMeet\`.

> **Poznámka:** složka `wwwroot` musí zůstat vedle `KioskMeet.exe`,
> jinak appka nenajde `index.html`.

### Automatické spuštění při startu Windows

Doporučujeme Naplánované úlohy (Task Scheduler) – umožňují spustit
appku hned po přihlášení a případně ji automaticky restartovat, kdyby
spadla:

1. Otevřete **Naplánované úlohy** → **Vytvořit úlohu…**
2. **Obecné**: název `KioskMeet`.
3. **Triggery** → Nový → *Při přihlášení*.
4. **Akce** → Nová → Spustit program: `C:\KioskMeet\KioskMeet.exe`
5. **Nastavení** → zaškrtněte „Pokud úloha selže, restartovat
   každou…" (např. 1 minutu) pro odolnost proti pádu appky.

Alternativně stačí vložit zástupce na `KioskMeet.exe` do složky
`shell:startup` daného uživatelského účtu.

## Jak appka funguje

Appka se skládá ze tří oken:

- **Hlavní okno** – fullscreen bezokrajové okno, které zobrazuje
  úvodní obrazovku (`wwwroot/index.html`) s výběrem služby a zadáním
  kódu/odkazu. Formulář posílá data appce přes
  `window.chrome.webview.postMessage()`.
- **Okno schůzky** – appka po odeslání formuláře otevře samostatné
  okno a naviguje na příslušnou URL (Meet/Teams/Zoom). Sleduje, kdy
  stránka opustí doménu dané služby (typicky přesměrování po skončení
  hovoru) nebo kdy skončí proces nativní appky (Teams/Zoom klient) – v
  obou případech se okno **automaticky zavře** a appka se vrátí na
  úvodní obrazovku. Pro jistotu je v okně i tlačítko „✕ Ukončit a
  vrátit se" pro ruční návrat.
- **Ovládací lišta** – malé samostatné vždy-navrch okno v pravém horním
  rohu se třemi tlačítky:
  - **🏠 Domů** – vrátí úvodní obrazovku do popředí, aniž by ukončilo
    právě probíhající schůzku.
  - **🔄 Restart** – po potvrzení appku restartuje (pro zotavení z
    chybového stavu).
  - **⏻ Ukončit** – po potvrzení (s upozorněním, že se to
    nedoporučuje) appku úplně vypne. Stejné potvrzení platí i pro
    skrytou klávesovou zkratku `Ctrl+Alt+X`.

  Lišta zůstává viditelná i nad oknem schůzky díky pravidelnému
  přeřazení navrch (`ControlBarWindow.ReassertTopmost()`), protože dvě
  souběžná "vždy navrch" okna by se jinak mohla překrývat podle toho,
  které bylo naposledy aktivní.

### Podpora nativních appek Teams a Zoom

Když stránka Teams/Zoom chce přesměrovat na svůj vlastní protokol
(`msteams:`, `zoommtg:`), appka to zachytí a předá Windows shellu:

- **Appka je nainstalovaná** → Windows ji spustí jako běžnou
  desktopovou appku; kiosk pak hlídá běžící proces a po jeho ukončení
  se vrátí na úvodní obrazovku.
- **Appka není nainstalovaná** → pokus tiše selže a uživatel zůstává
  na webové verzi, kde si Teams/Zoom samy nabídnou pokračování v
  prohlížeči.

> **Známé omezení:** desktop klient Zoomu po instalaci často běží na
> pozadí (v systémové liště) i mimo aktivní hovor. Appka pak může
> chybně vyhodnotit „proces stále běží" i po skončení schůzky. Pro
> tyto případy zůstává vždy k dispozici tlačítko „✕ Ukončit a vrátit
> se" přímo v okně schůzky.

### Zjednodušené zadávání u Teams

Protože Microsoft (na rozdíl od Zoomu) nepodporuje sestavení odkazu
jen z ID schůzky přes parametry v URL, appka při zadání samotného ID:

1. Otevře oficiální stránku Microsoftu pro připojení podle ID.
2. Zkusí pole „ID schůzky" / „Heslo" vyplnit sama (hledá je podle
   placeholderu, ne podle pevného ID prvku – odolnější vůči drobným
   změnám stránky, ale ne stoprocentně garantované, protože jde o
   veřejnou stránku mimo naši kontrolu).
3. Pro jistotu zároveň zkopíruje ID/heslo do schránky a zobrazí je v
   panelu v okně schůzky – když se automatické vyplnění nepovede,
   stačí kliknout do pole a stisknout `Ctrl+V`.

## Konfigurace

### Branding a základní nastavení (config.json)

Logo, název appky a pár dalších věcí se nastavuje bez zásahu do kódu
přes `wwwroot/config.json`. Stejný soubor čte jak C# (např. pro titulek
okna), tak JavaScript v `index.html` (přes `fetch`), takže stačí
upravit jeden soubor.

```json
{
  "appName": "Meeting Kiosk",
  "windowTitle": "Meeting Kiosk",
  "tagline": "Připojení na online schůzku",

  "logoPath": "",
  "logoAltText": "MK",

  "accentColor": "#f5a623",
  "accentColorDark": "#f07d1a",

  "helpContactText": "V případě potíží kontaktujte IT oddělení.",

  "audioDeviceNameContains": "Jabra",
  "audioDeviceLabel": "Jabra 810"
}
```

| Klíč | Co dělá |
|---|---|
| `appName` | Zobrazí se jako textové logo v levém panelu, pokud není nastavený `logoPath`. |
| `windowTitle` | Titulek okna appky (a `<title>` stránky). |
| `tagline` | Podnadpis pod logem. |
| `logoPath` | Cesta k obrázku loga **relativní vůči `wwwroot/`** (např. `assets/logo.png`). Když je prázdná, appka zobrazí místo obrázku textové logo (`appName`). |
| `logoAltText` | Alternativní text obrázku loga (pro přístupnost). |
| `accentColor`, `accentColorDark` | Barvy hlavního tlačítka „Připojit se" a zvýraznění. |
| `helpContactText` | Text na konci bočního panelu s nápovědou (např. kontakt na IT). |
| `audioDeviceNameContains` | Podle jaké části názvu appka hledá preferované zvukové zařízení, které nastaví jako výchozí. |
| `audioDeviceLabel` | Popisek zvukového zařízení zobrazený ve stavovém panelu. |

Chcete-li použít vlastní logo, dejte obrázek do `wwwroot/assets/` (tuto
podsložku si vytvořte) a v `config.json` nastavte např.
`"logoPath": "assets/logo.png"`.

Pokud `config.json` chybí nebo je poškozený, appka bez pádu použije
vestavěné výchozí hodnoty.

### Pokročilejší úpravy (přímo v kódu)

| Co chcete upravit | Kde |
|---|---|
| Domény, po jejichž opuštění appka usoudí, že schůzka skončila | `AllowedHostsByService` v `MeetWindow.xaml.cs` |
| Názvy procesů nativních appek, které appka hlídá | `NativeProcessNamesByService` v `MeetWindow.xaml.cs` |
| Vzhled a texty jednotlivých stránek služeb | `wwwroot/index.html` |
| Klávesová zkratka pro ukončení appky (výchozí `Ctrl+Alt+X`) | `MainWindow_PreviewKeyDown` v `MainWindow.xaml.cs` |

## Kontrola periferií (kamera + mikrofon)

V rohu obrazovky appka zobrazuje stavový panel se dvěma řádky:

- **Kamera** – zjišťuje se přes WMI (`Win32_PnPEntity`, třída
  `Camera`/`Image`). Zelená tečka = nalezena a funkční, oranžová =
  nalezena, ale s problémem (např. chybí ovladač), červená =
  nenalezena.
- **Zvukové zařízení** – appka hledá konkrétní USB
  reproduktor/mikrofon podle části názvu (výchozí konfigurace hledá
  „Jabra" – upravitelné, viz [Konfigurace](#konfigurace)) a
  **automaticky ho nastaví jako výchozí** zařízení (mikrofon i
  reproduktor), pokud jím právě není.

Kontrola běží při startu appky a pak každých 10 sekund, takže se stav
sám opraví i po přehození USB kabelu.

### Jak funguje automatické přepnutí výchozího zařízení

Windows oficiálně nevystavuje veřejné API pro programové přepnutí
výchozího audio zařízení. Appka proto používá stejné nedokumentované
COM rozhraní (`IPolicyConfig`), jaké používají běžné nástroje pro
přepínání výchozího zařízení (např. EarTrumpet) – viz
`AudioDeviceHelper.cs`. Je to spolehlivé na Windows 10/11, ale jde o
nedokumentované API – pokud by se na konkrétním buildu Windows
nechovalo spolehlivě, appka to bezpečně odchytí a stavový panel ukáže
oranžovou tečku „připojeno, není výchozí". V tom případě je potřeba
nastavit zařízení jako výchozí ručně přes **Nastavení Windows →
Zvuk**.

### Povolení kamery/mikrofonu ve schůzce

Okno schůzky má vlastní `PermissionRequested` handler, který
automaticky povolí přístup ke kameře a mikrofonu pro Meet/Teams/Zoom
bez vyskakovacího dialogu prohlížeče. Ostatní oprávnění appka rovnou
zamítá.

## Testování připojení

- **Zoom** – appka obsahuje tlačítko „🎥 Otestovat zvuk a obraz",
  které otevře oficiální testovací schůzku
  [zoom.us/test](https://zoom.us/test) – stejnou cestou (stejné okno,
  stejná logika automatického povolení kamery/mikrofonu i
  automatického návratu), jakou appka používá pro reálné schůzky.
- **Teams** – Microsoft veřejnou testovací schůzku nenabízí.
  Doporučujeme v libovolném Teams účtu vytvořit rychlou schůzku
  „Sejít se nyní" a vyzkoušet ji přes kiosk.

### Kontrolní seznam po nasazení

- [ ] Google Meet: zadání kódu → otevře se okno, kamera/mikrofon bez
      vyskakovacího dialogu, po odchodu ze schůzky se okno samo zavře.
- [ ] Zoom: „Otestovat zvuk a obraz" → funguje mikrofon/kamera/reproduktor.
- [ ] Zoom: zadání ID + hesla → připojí se do reálné schůzky.
- [ ] Teams: vložení celého odkazu → připojí se.
- [ ] Teams: zadání jen ID + hesla → appka otevře stránku a zkusí pole
      vyplnit sama; pokud ne, hodnoty jsou zkopírované ve schránce.
- [ ] Chytré vložení: vložení celého textu pozvánky do pole → appka
      pozná službu a vytáhne jen odkaz.
- [ ] Stavový panel: kamera i zvukové zařízení svítí zeleně.
- [ ] Ovládací lišta (Domů/Restart/Ukončit) je viditelná i během
      schůzky.
- [ ] Pokud je nainstalovaný nativní Teams/Zoom klient, appka se ho
      pokusí spustit místo webové verze.

## Zástupci na ploše

Ve složce `shortcuts/` je skript `Create-DesktopShortcuts.vbs`, který
na ploše vytvoří dva zástupce:

- **Chrome (host)** – spustí Chrome v režimu Host (`--guest`), tedy
  čistý profil bez přihlášení, historie a uložených hesel.
- **Spustit kiosk** – spustí `KioskMeet.exe`.

### Použití

1. Otevřete `shortcuts/Create-DesktopShortcuts.vbs` v textovém
   editoru a upravte proměnnou `kioskExePath` na skutečnou cestu, kam
   jste appku nainstalovali.
2. Poklepejte na soubor – zástupci se vytvoří na ploše aktuálního
   uživatele.
3. Skript se zeptá, jestli chcete zástupce vytvořit i pro **všechny
   uživatele počítače** (sdílená plocha) – to vyžaduje spuštění jako
   Správce (z příkazové řádky spuštěné jako Správce: `cscript
   Create-DesktopShortcuts.vbs`).

Cesta k Chromu se hledá automaticky (`Program Files` i `Program Files
(x86)`); pokud by na cílovém PC byla jinde, upravte proměnné
`chromePath` / `chromePathX86` na začátku skriptu.

## Vydávání nových verzí

Repozitář obsahuje GitHub Actions workflow
(`.github/workflows/release.yml`), který automaticky sestaví
self-contained Windows build a přiloží ho jako soubor ke GitHub
Release při vytvoření a nahrání tagu ve tvaru `vX.Y.Z`:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Za pár minut se v záložce **Releases** objeví nová verze se ZIP
souborem obsahujícím appku, `wwwroot/` i pomocné skripty ze
`shortcuts/` – uživatel tak nemusí mít nainstalovaný .NET SDK ani
appku sám kompilovat.

Workflow jde spustit i ručně (bez tagu) přes GitHub UI → záložka
**Actions** → *Build and Release (Windows)* → *Run workflow* – hodí
se pro rychlé otestování buildu bez vytváření oficiální verze.

Doporučujeme [Semantic Versioning](https://semver.org/)
(`MAJOR.MINOR.PATCH`): MAJOR pro zásadní změny architektury, MINOR pro
nové funkce se zachovanou zpětnou kompatibilitou, PATCH pro opravy
chyb.

## Podpora platforem

| Platforma | Stav |
|---|---|
| Windows 10/11 | ✅ Podporováno |
| Linux | 🗺️ Na roadmapě |

Appka je postavená na WPF a Microsoft WebView2 – technologiích
vázaných na Windows (WebView2 runtime, COM rozhraní pro správu audio
zařízení, WMI pro kontrolu kamery, registrace URL protokolů pro
nativní handoff na Teams/Zoom). Linux podpora by vyžadovala jednu ze
dvou cest:

1. **Odlehčená webová verze** – `wwwroot/index.html` puštěné v
   Chromium `--kiosk` na Linuxu, ale bez nativních funkcí (automatické
   přepnutí zvukového zařízení, kontrola kamery, nativní Teams/Zoom
   handoff, sledování procesů).
2. **Plný přepis na cross-platform framework** (např. Avalonia UI
   místo WPF) se zachováním všech funkcí i na Linuxu – výrazně větší
   rozsah práce.

## Licence

[MIT](LICENSE) – appku můžete volně používat, upravovat i dále šířit.
