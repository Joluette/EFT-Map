# Release 3.3.2 documentation images

The gameplay capture was supplied by the project author; a notification containing a private path was redacted using imagegen. UI previews use synthetic demo data. Map artwork remains credited to Shebuka under CC BY-NC-SA 4.0, game artwork belongs to Battlestate Games and the respective rights holders. See [image credits](docs/images/README.md). The banner uses original code-drawn typography and contours.

---

# Źródła i licencje — EFT Position 3

Sprawdzono 26.10.2026. Kalibracja jest przypięta do podanych rewizji. Aplikacja pobiera brakujące obrazy i dokładniejsze kafelki widocznego fragmentu trzech map oraz dodatkowych pięter Customs i Reserve. Dziesięć map bazowych jest dołączonych jako SVG.

## Kod i format zrzutów

Format nazwy zweryfikowano w **the-hideout/TarkovMonitor**, autorzy i współtwórcy, rewizja `3cf361efc49fecf6bffce3cae567f55a303757cd`:

- [GameWatcher.cs](https://github.com/the-hideout/TarkovMonitor/blob/3cf361efc49fecf6bffce3cae567f55a303757cd/TarkovMonitor/GameWatcher.cs), `ScreenshotWatcher_Created`, `QuarternionsToYaw`.
- [GPL v3](https://github.com/the-hideout/TarkovMonitor/blob/3cf361efc49fecf6bffce3cae567f55a303757cd/LICENSE.txt), pełna kopia w `licenses/GPL-3.0.txt`.
- Parser prototypu ma własną walidację całej nazwy, daty, liczb i kwaternionu oraz sufiksy wielocyfrowe. Kierunek jest obliczany przez obrót wektora forward i rzut na XZ, aby uwzględnić pochylenie kamery.

Kod prototypu udostępniono na **GPL-3.0-only**, pełny tekst w `LICENSE.txt`. Źródła, manifesty oraz narzędzia budowania są dostarczone wraz z EXE. TarkovMonitor nie jest zależnością wykonywalną aplikacji.

## Dane i kalibracja wszystkich map

### Znaczniki questów i ekstraktów — migawka z 27.10.2026

Dodatkowa nakładka korzysta z publicznego [JSON API tarkov.dev](https://tarkov.dev/api), utrzymywanego przez społeczność tarkov.dev. Pobrano dane `maps`, `tasks` i słowniki nazw z `https://json.tarkov.dev` dla `regular`, `pve` i `pvp-season`. Oficjalna dokumentacja udostępnia API do budowania własnych narzędzi; wskazuje dane pochodzące m.in. z Tarkov Changes, Escape from Tarkov Wiki, TarkovTracker/tarkovdata i sieci skanerów. Nie przypisujemy licencji kodu witryny do wszystkich źródłowych danych lub grafik.

`assets/markers.json` zawiera zredukowane nazwy i współrzędne potrzebne aplikacji, bez opisów zadań, nagród czy obrazów. `assets/markers.source.json` zapisuje adresy źródłowe, datę pobrania, sumy SHA-256 i pokrycie map. Narzędzia `Prepare-Markers.cjs` i `Audit-Markers.cjs` przygotowują dane i sprawdzają ich powiązanie z pobranymi rekordami źródłowymi. Wyszukiwarka działa lokalnie i nie wysyła zapytań użytkownika ani zrzutów do API.

### Kalibracja map i metadane pięter

Projekt **the-hideout/tarkov-dev**, Copyright (c) 2019 **Oskar Risberg** i współtwórcy. Rewizja `ec44177bd3c21aa2b33938153acd0a96dbd939aa`:

- [maps.json](https://github.com/the-hideout/tarkov-dev/blob/ec44177bd3c21aa2b33938153acd0a96dbd939aa/src/data/maps.json): granice, obroty, transformacje, opisy, autorzy, piętra i publiczne adresy kafelków.
- [Implementacja mapy](https://github.com/the-hideout/tarkov-dev/blob/ec44177bd3c21aa2b33938153acd0a96dbd939aa/src/pages/map/index.jsx): `getCRS`, `applyRotation`, `pos`, `svgBounds`, `svgLayer` i `tileSize`.
- **MIT** — pełne pozwolenie i copyright zachowano w `licenses/tarkov-dev-MIT.txt`.

Oryginalne dane dołączono jako `assets/maps.upstream.json`; katalog i macierze wyprowadzono jako `assets/maps.json`. Wybrano 13 lokacji i 55 poziomów, w tym kafelkowe piętra niedostępne w SVG. Źródłowe wysokości i obszary budynków służą automatycznej nawigacji. Nie dołączono Leaflet ani przeglądarki. Licencja MIT danych lub strony **nie jest przenoszona na cudze obrazy**.

## Dziesięć dołączonych map wektorowych

Autor wskazany w danych: **Shebuka**; repozytorium **the-hideout/tarkov-dev-svg-maps**, jego autorzy i współtwórcy. Rewizja `5a8b6115d1c0cf56f2ebaac1a96fa5ae3074d178`:

- [Oryginalne mapy](https://github.com/the-hideout/tarkov-dev-svg-maps/tree/5a8b6115d1c0cf56f2ebaac1a96fa5ae3074d178): Customs, Factory, GroundZero, Interchange, Lighthouse, Reserve, Shoreline, StreetsOfTarkov, Terminal, Woods.
- [Licencja](https://github.com/the-hideout/tarkov-dev-svg-maps/blob/5a8b6115d1c0cf56f2ebaac1a96fa5ae3074d178/LICENSE.md): **CC BY-NC-SA 4.0** — [Creative Commons](https://creativecommons.org/licenses/by-nc-sa/4.0/), pełny tekst `licenses/map-CC-BY-NC-SA-4.0.txt`.
- [README autora](https://github.com/the-hideout/tarkov-dev-svg-maps/blob/5a8b6115d1c0cf56f2ebaac1a96fa5ae3074d178/README.md) zawiera dodatkowo zakaz wykorzystania w narzędziach do oszukiwania, radarach/ESP, klientach cheatów i automatyzacji oraz opisaną przez autora klauzulę odwołania pozwolenia. Pełny niezmieniony tekst zachowano w `licenses/map-upstream-README.md`, osobno od tekstu CC.

**Modyfikacje z 26.10.2026:** wybrano warstwy bazowe i piętra, zachowano grupy `data-keep-with-group`, przeniesiono wybraną warstwę ponad bazę zgodnie z kolejnością nakładek. Przygotowano 30 SVG w `assets/vectors` i obrazy PNG. Zachowano orientację, geometrię, `xMidYMid meet` oraz kalibrację. W czasie ładowania deklaracje klas CSS są przepisywane na atrybuty odpowiednich elementów, a polecenia ścieżek na równoważne bezwzględne polecenia; usuwa to błędy dziedziczenia i błędne przekątne starszego renderera bez usuwania geometrii mapy. Narzędzia: `tools/prepare-maps.py`, `tools/render-maps.cjs`; kod normalizacji: `src/VectorMap.cs`, `src/SvgPathCanonicalizer.cs`.

Wynikowe grafiki w `assets/maps`, `assets/vectors` i podglądy w `reports` zachowują **CC BY-NC-SA 4.0**. Wymagają atrybucji, użycia niekomercyjnego i udostępniania adaptacji grafiki na tej samej licencji. Oryginały są w `assets/sources`; `Labs.svg` jest materiałem źródłowym, nie kalibracją wyświetlanego The Lab. Własny znacznik i etykiety są rysowane osobno. W pełnym ekranie informacja o autorze pozostaje dostępna pod prawym przyciskiem oraz w dokumentacji.

## Trzy mapy pobierane do osobistego podglądu

| Mapa | Autor wskazany przez tarkov.dev | Źródło kafelków |
|---|---|---|
| Icebreaker | **TarkovBOT.eu** — [strona autora](https://tarkovbot.eu/tools/interactive-maps/icebreaker) | `https://assets.tarkov.dev/maps/icebreaker/…` |
| The Lab / Dark | **Tarkov.dev** — [mapa](https://tarkov.dev/map/the-lab) | `https://assets.tarkov.dev/maps/labs_v4/…` |
| The Labyrinth | **Tarkov.dev** — [mapa](https://tarkov.dev/map/the-labyrinth) | `https://assets.tarkov.dev/maps/labyrinth/…` |
| Dodatkowe piętra Customs i Reserve | Autorzy wskazani przez dane tarkov.dev; nie utożsamiamy kafelków satelitarnych z grafiką SVG Shebuki | `https://assets.tarkov.dev/maps/customs_0.16/…`, `https://assets.tarkov.dev/maps/reserve/…` |

Ich adresy i kalibrację wskazuje podany wyżej publiczny plik `maps.json`. **Nie potwierdzono publicznej licencji pozwalającej na ich swobodną redystrybucję. Nie obejmujemy ich licencją MIT ani licencją CC map wektorowych. Dlatego obrazów tych trzech map nie ma w dystrybuowanym ZIP.**

Po wybraniu mapy aplikacja pobiera kafelki z publicznych adresów, składa wybrany poziom w lokalny plik cache do osobistego podglądu i pokazuje nazwę autora. Użytkownik nie otrzymuje od tej aplikacji dodatkowego pozwolenia na dalsze rozpowszechnianie tych grafik. Udostępniając program dalej, korzystaj z dostarczonego ZIP, bez dodawania własnego `cache` tych map.

Nie jest obchodzone logowanie ani kontrola dostępu. Nie przesyła się zrzutów ani ich nazw. Numery żądanych kafelków opisują jednak widoczny obszar mapy; przy automatycznym centrowaniu może to pośrednio wskazywać okolicę pozycji w grze. Dostępność źródła oraz jego warunki mogą się zmienić. Obrazy pobrane podczas QA pozostały poza paczką.

## SharpVectors — natywne rysowanie SVG

**SharpVectors 1.8.5**, Copyright (c) 2010–2024, **Elinam LLC**, zgodnie z dołączoną licencją. [Projekt](https://github.com/ElinamLLC/SharpVectors), [pakiet NuGet](https://www.nuget.org/packages/SharpVectors/1.8.5).

Używane są wyłącznie biblioteki Core, Dom, Css, Model i Rendering.Gdi dla .NET Framework. Bez WPF, osadzonej przeglądarki i osobnych procesów. Renderer rysuje do bufora o wymiarach widocznego okna, zamiast tworzyć ogromną bitmapę całej mapy.

Licencja: **BSD-3-Clause**, pełny oryginalny tekst wraz z informacjami o dołączonych składnikach **Brotli** i **MiniIoC** na MIT zachowano w `licenses/SharpVectors-1.8.5-BSD-3-Clause.txt`. W `lib/dependencies.json` podano adres i SHA-256 pakietu oraz każdej z pięciu bibliotek. Te informacje należy zachować przy dalszym rozpowszechnianiu. Własny kod integracji i normalizacji pozostaje na GPL-3.0-only.

## Narzędzia i nazwy

Rasteryzację przeprowadzono pakietem `sharp`/libvips dostępnym w środowisku. Nie jest dystrybuowany ani potrzebny do działania aplikacji. Opcjonalne skrypty przygotowania grafiki wymagają jego osobnej instalacji. CLR, WinForms i System.Drawing pochodzą z zainstalowanego .NET Framework.

Escape from Tarkov i nazwy lokacji należą do odpowiednich właścicieli, w tym Battlestate Games. To samodzielny podgląd własnej pozycji z pliku, nie nakładka radarowa ani odczyt informacji o innych graczach. Autorzy map, tarkov.dev i wydawca gry nie są przedstawiani jako sponsorzy ani podmioty zatwierdzające prototyp.

## Tła interfejsu dodane w 3.2.0

Pliki `assets/tarkov-background.jpg` oraz `assets/tarkov-cyberpunk-background.jpg` mają 1672×941 pikseli. To rekonstrukcje graficzne wygenerowane z użyciem AI na podstawie referencji dostarczonych przy tworzeniu projektu, nie oryginalne materiały 4K. Nie przedstawiamy ich jako oficjalnej grafiki Battlestate Games ani CD PROJEKT RED lub jako potwierdzenia współpracy marek. Nazwy i elementy rozpoznawalnych marek należą do odpowiednich właścicieli. Licencja GPL kodu aplikacji ani licencje map nie nadają dodatkowych praw do tych elementów.

SHA-256: Tarkov `2ee58c3e30e865be6c2aa61f53bd332942589b8721e4de5ae63d66d18ec5fe7c`; Tarkov × Cyberpunk `170c1076a86b054a4ca22b24855f7fa67e068cc2b9c6b812f987f21fe6fbbf61`. Użytkownik może wybrać jednolity kolor lub zaimportować własne tło w ustawieniach.
