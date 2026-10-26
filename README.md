# EFT Position 3 — ostre mapy, automatyczne wnętrza, pełny ekran

Aplikacja C# / WinForms na Windows x64 z .NET Framework 4.8. Pokazuje Twoją pozycję i kierunek z **ostatniego zrzutu EFT**, bez czytania pamięci ani logów gry. Działa w jednym procesie, bez przeglądarki i bez WPF.

## Gotowy program czy kod źródłowy?

**Gotowy program:** otwórz [wydanie v3.0.0](https://github.com/Joluette/EFT-Map/releases/tag/v3.0.0), rozwiń **Assets** i pobierz **EFT-Position-Windows-v3-GitHub.zip**. Ten ZIP zawiera aplikację i wszystkie potrzebne pliki.

**Kod źródłowy:** pliki z tego repozytorium oraz automatyczny „Source code (zip)” wymagają zbudowania. Rozpakuj kod do folderu, kliknij **Zbuduj.cmd**, a po udanej kompilacji **Uruchom.cmd**. Zachowaj foldery `assets`, `lib`, `licenses` i plik `EftPosition.exe.config`. Wystarczy Windows x64 z .NET Framework 4.8; zwykłe budowanie nie potrzebuje internetu, NuGet, Node ani Pythona.

Katalog `reports` zawiera wyniki badania wersji 3. Publiczna paczka ma zanonimizowane lokalne ścieżki. Syntetyczny podgląd pełnego ekranu: [reports/preview-v3-fullscreen.png](reports/preview-v3-fullscreen.png).

## Uruchomienie

1. Rozpakuj **cały ZIP do nowego folderu**, np. w Dokumentach. Zachowaj `assets`, `lib` i plik `EftPosition.exe.config` obok programu.
2. Uruchom `EftPosition.exe`, `Uruchom.cmd` albo skrót na pulpicie wskazujący tę instalację.
3. W menu **„Mapa”** wybierz lokację, na której grasz. Opcja **„Auto wnętrza”** jest domyślnie włączona.
4. Kliknij **„Wybierz folder…”** i wskaż folder zrzutów zapisanych przez EFT. Zwykle to `Dokumenty\Escape from Tarkov\Screenshots`, ale wybierz rzeczywistą lokalizację.
5. Wykonuj zrzuty ręcznie funkcją gry. Program aktualizuje pozycję oraz — gdy są odpowiednie dane — piętro i zbliżenie.

**Bez gry:** `Przyklad.cmd` pokazuje dane przykładowe. Dołączony plik ma pustą zawartość, ponieważ aplikacja odczytuje nazwę i metadane, a nie obraz screenshotu.

## Co zmieniono w wersji 3

**Ostrość:** dziesięć map bazowych jest rysowanych bezpośrednio z geometrii SVG. Przy przybliżaniu linie i pomieszczenia są ponownie rysowane w aktualnej skali ekranu, zamiast powiększania starego PNG. Zapamiętywany jest tylko obraz bieżącego widoku, wielkości okna.

Mapy i piętra dostępne wyłącznie jako kafelki dobierają teraz dokładniejsze kafelki do przybliżenia i pobierają tylko aktualnie widoczny obszar. Po zbliżeniu szczegóły mogą pojawić się z krótkim opóźnieniem. Jakość kończy się na najwyższym poziomie udostępnianym przez autora źródła (`MaxTileZoom`); program nie wymyśla szczegółów, których nie ma w obrazie.

**Automatyczne wnętrza:** nowe współrzędne X/Y/Z ze zrzutu mogą przełączyć piętro, wyśrodkować i dopasować widok budynku lub podziemi. Nie oznacza to śledzenia pozycji pomiędzy zrzutami.

**Pełny ekran:** przycisk **„Pełny ekran (F11)”** albo klawisz **F11** pokazuje samą mapę na całym ekranie. **Esc** lub ponowne **F11** przywraca okno. Możesz też kliknąć mapę prawym przyciskiem i wybrać powrót z menu.

## Jak działa „Auto wnętrza”

Po odczytaniu zrzutu aplikacja porównuje X/Z z obszarami budynków, a Y z zakresami wysokości pięter. W pierwszej kolejności korzysta z danych obszarów i wysokości tarkov.dev. Tam, gdzie brakuje takiego opisu, może użyć prostokątnego obrysu geometrii oznaczonej w SVG jako budynek, np. na Woods, Lighthouse lub Terminal.

Po wejściu do nowego rozpoznanego wnętrza widok dopasowuje jego obszar. Po zmianie piętra wybiera właściwą warstwę; po wyjściu wraca do widoku ogólnego. Kolejne zrzuty w tym samym wnętrzu nie wymuszają od nowa tego samego zbliżenia. Niewielka tolerancja na granicach ogranicza przeskakiwanie między poziomami, gdy współrzędne wahają się przy progu.

Obrys z grafiki jest **przybliżeniem**, nie odczytem kolizji gry: nieregularny budynek może zajmować tylko część swojego prostokąta, a dane wysokości nie opisują idealnie wszystkich miejsc. Automatyczne rozpoznanie nie jest gwarantowane dla każdego pomieszczenia i mapy. W razie nietrafnego wyboru odznacz **„Auto wnętrza”** i użyj menu **„Poziom”**. Ręczny wybór piętra również wyłącza automat, aby nie zmieniał Twojego wyboru; możesz go później ponownie włączyć.

Opcja **„Śledź pozycję”** ma inne zadanie: centruje znacznik po każdym zrzucie. Włącz ją, jeśli chcesz stale wracać do własnej pozycji. Domyślnie jest wyłączona, aby móc swobodnie przesuwać mapę.

**Samą mapę nadal wybierasz ręcznie.** Nazwa PNG zawiera X/Y/Z i obrót, ale nie identyfikator lokacji. Współrzędne różnych map się nakładają. Program nie zgaduje lokacji ani nie czyta dodatkowych danych gry, żeby ją rozpoznać.

## Mapy i poziomy

| Mapa | Poziomy w menu | Obraz |
|---|---:|---|
| Customs | 5 | Baza i trzy poziomy SVG offline; 4th Floor pobierany |
| Factory | 4 | SVG offline |
| Ground Zero | 4 | SVG offline |
| Icebreaker | 16 | Kafelki pobierane |
| Interchange | 3 | SVG offline |
| Lighthouse | 1 | SVG offline |
| Reserve | 6 | Baza i bunkry SVG offline; piętra 2nd–5th pobierane |
| Shoreline | 4 | SVG offline |
| Streets of Tarkov | 6 | SVG offline |
| Terminal | 1 | SVG offline |
| The Lab / Dark | 3 | Kafelki pobierane |
| The Labyrinth | 1 | Kafelki pobierane |
| Woods | 1 | SVG offline |

Łącznie: **13 map i 55 pozycji poziomów**. Nazwy pochodzą z katalogu autorów, w tym „Stairs (blocked)” dla zablokowanych schodów Icebreaker. Ground Zero ma wspólną geometrię wariantów doboru graczy. The Lab / Dark używa wspólnej kalibracji i obrazu, bez oddzielnej grafiki oświetlenia.

Zmiana mapy przelicza ostatnią znaną pozycję na nowy obraz i zachowuje czas pliku. Upewnij się, że wybrałeś właściwą lokację. Program nie potrafi stwierdzić, z której mapy pochodzi stary screenshot. Pozycja poza widocznym obszarem daje komunikat, nie jest przyklejana do krawędzi.

## Obsługa i czas ostatniej pozycji

- Kółko myszy przybliża w miejscu kursora; przeciągnięcie lewym przyciskiem przesuwa mapę.
- `+` / `−` zmieniają przybliżenie, **„Cała mapa”** przywraca pełny widok, **„Do znacznika”** centruje pozycję.
- **F11** przełącza pełny ekran; **Esc** wraca do okna. Menu pod prawym przyciskiem działa także przy ukrytych panelach.
- **„Zatrzymaj”** zatrzymuje obserwację folderu i pozostawia ostatni znacznik z jego wiekiem.
- Turkusowy znacznik oznacza plik młodszy niż 30 s, starszy jest bursztynowy. Linia i klin pokazują kierunek; klin nie odtwarza rzeczywistego FOV gry.

W zwykłym oknie panel pokazuje lokalny **czas modyfikacji pliku**, datę i wiek pozycji. Kopiowanie screenshotu lub zmiana jego daty przez inne narzędzie może zmienić ten wiek. W pełnym ekranie panel jest ukryty; czas ostatniego zrzutu i autor mapy są dostępne pod prawym przyciskiem myszy. Obserwacja folderu i kolor starego znacznika działają dalej. F11 i Esc działają tylko w oknie tej aplikacji; nie ma globalnych skrótów ani wysyłania klawiszy do EFT. Można też uruchomić program z parametrem `--fullscreen`.

## Internet, cache i prywatność

Trzy mapy kafelkowe oraz dodatkowe piętra Customs i Reserve potrzebują internetu przy pierwszym pobraniu obrazu. Dokładniejsze kafelki są pobierane podczas zbliżania i przesuwania widoku, a później odczytywane z lokalnego podfolderu `cache`. Przy braku internetu dostępny pozostaje materiał już zapisany na dysku i mapy SVG.

W pamięci przechowuje się najwyżej **24 MiB rozkodowanych szczegółowych kafelków**. To limit tego konkretnego cache, nie całego programu. Część buforów może być potrzebna do dekodowania i rysowania; zużycie RAM całej aplikacji wraz z procesami sprawdza `RAPORT.md`. Nie są wczytywane jednocześnie wszystkie mapy ani cała historia zrzutów.

Program nie wysyła plików screenshotów ani ich nazw. Żądania kafelków zawierają jednak identyfikator źródła i numery kafelków **widocznego fragmentu mapy**, więc serwer może wywnioskować, jaki obszar mapy oglądasz. Przy automatycznym centrowaniu może to pośrednio wskazywać okolicę pozycji w grze. To nie jest odczyt obrazu zrzutu ani przesyłanie go do serwera.

Nie potwierdzono publicznej licencji pozwalającej nam swobodnie przepakowywać pobierane grafiki, dlatego tych obrazów nie ma w dystrybuowanym ZIP. Są pobierane do osobistego podglądu z publicznego źródła; ich autorzy są wskazani w dokumentacji. Nie dołączaj własnego cache do paczki przekazywanej innym. Szczegóły: `THIRD-PARTY-NOTICES.md`.

## Pliki i bezpieczeństwo działania

Aplikacja obserwuje PNG w wybranym folderze, bez podfolderów. Czyta nazwę i metadane, nie otwiera zawartości screenshotu. Używa `FileSystemWatcher`, scala powtórzenia, przechowuje ostatnią pozycję i ograniczone bufory. Po utracie folderu ponawia połączenie, a po przepełnieniu powiadomień wykonuje ponowny skan. Duża seria aktualizacji pokazuje najnowszy stan zamiast odtwarzać historię.

Program **nie usuwa ani nie modyfikuje zrzutów**, nie czyta pamięci procesu ani logów EFT i nie generuje naciśnięć klawiszy. Może usuwać własne niedokończone pliki pobierania w cache. Rozpakuj program do folderu, w którym może zapisywać pliki, np. do Dokumentów zamiast `Program Files`.

## Kod źródłowy i pierwsza zmiana

Zamknij aplikację i kliknij **`Zbuduj.cmd`**. Budowanie korzysta z kompilatora dostarczanego z .NET Framework i bibliotek dołączonych w `lib`; nie pobiera NuGet ani nie wymaga SDK `dotnet` lub Visual Studio. Opcjonalnie otwórz `EftPosition.csproj` w Visual Studio z obsługą .NET Framework 4.8.

| Plik | Zadanie |
|---|---|
| `src/Core.cs`, `FolderMonitor.cs` | Nazwy plików, pozycja i obserwacja folderu |
| `src/Maps.cs` | Katalog oraz przeliczenie współrzędnych i kierunku |
| `src/AutoNavigation.cs` | Obszary wnętrz, wysokość pięter i tolerancja granic |
| `src/VectorMap.cs` | Ostre rysowanie SVG do bufora wielkości widoku |
| `src/TileMap.cs`, `MapAssets.cs` | Pobieranie i ograniczony cache kafelków |
| `src/MapCanvas.cs` | Rysowanie, dopasowanie, przybliżanie i przesuwanie |
| `src/MainForm.cs` | Menu, automat, pełny ekran i czas aktualizacji |
| `assets/maps.json`, `maps.upstream.json` | Kalibracja oraz źródłowe zakresy i wysokości |

Pierwsze ćwiczenie: zmień tekst przycisku w `MainForm.cs`, zamknij program i uruchom `Zbuduj.cmd`. Do działania zachowaj cały `lib` i plik `.exe.config`, który wskazuje miejsce szukania bibliotek. Skrypty CMD ustawiają zasady wykonywania PowerShell tylko dla swojego procesu, bez zmiany ustawień systemu.

### Format i przeliczanie

```text
2026-10-26[12-34]_200.00, 2.30, 150.00_0.00000, 0.70711, 0.00000, 0.70711 (0).png
                 X        Y       Z       qx       qy       qz       qw
```

Kierunek jest kwaternionem: parser normalizuje go, obraca wektor „do przodu” i rzutuje na XZ. Przy patrzeniu pionowo strzałka znika. Wysokość Y służy automatowi pięter. Złe nazwy, daty i liczby są pomijane; separator pozostaje kropką niezależnie od języka Windows.

Każdy poziom ma macierz `pixelX=A*X+B*Z+Tx`, `pixelY=C*X+D*Z+Ty`. Obejmuje ona obrót i skalę źródła. Factory ma obrót 90°, Labs i Labyrinth 270°, Reserve ma osobne `svgBounds`, a Icebreaker różne skale osi. Kierunek przechodzi przez tę samą macierz. Dla Customs zachowano `(698-X)*3`, `(Z+307)*3`: Dorms `(200,150)` wypada w `(1494,1371)` px. Kafelki Labs mają rozstaw 175 px, choć obrazy mogą mieć 256 px; są skalowane zgodnie z kalibracją źródła.

Oryginały są w `assets/sources`, przygotowane SVG poziomów w `assets/vectors`, a dane w `assets/maps.upstream.json`. Opcjonalne narzędzia przygotowania grafiki wymagają Python, Node i `sharp`; zwykłe budowanie C# i działanie programu ich nie wymagają.

## Testy, pamięć i licencje

`Testuj.cmd` buduje i uruchamia testy na nowych danych przykładowych: rdzeń, nawigację, kafelki offline, SVG i okno aplikacji. Testy okna wymagają zwykłej sesji pulpitu Windows. Test sieciowy `HiresNetworkTests.cs` jest dołączony jako dodatkowe źródło i nie należy do domyślnego uruchomienia `Testuj.cmd`. Dodatkowe testy źródłowe obejmują `AutoNavigationTests`, `TileMapTests`, `ExperienceTests` oraz `HiresNetworkTests`: sprawdzają m.in. zmianę pięter, tolerancję granic, ostrość, cache i pełny ekran. Testy sieciowe mogą wymagać pobrania kafelków. Wyniki, sposób pomiaru RAM i ograniczenia znajdują się w **`RAPORT.md`**; wcześniejszego wyniku wersji 2 nie należy traktować jako pomiaru tej wersji.

EFT nie było potrzebne do testów. Nie deklarujemy pomiaru dokładności w realnym nalocie ani zatwierdzenia przez wydawcę gry.

Kod: **GPL-3.0-only**. Dane tarkov.dev: **MIT, © 2019 Oskar Risberg i współtwórcy**. Dołączone mapy Shebuki: **CC BY-NC-SA 4.0**, z ograniczeniem użycia niekomercyjnego i zachowaniem atrybucji. Renderer **SharpVectors 1.8.5: BSD-3-Clause**, wraz z pełnymi informacjami o dołączonych składnikach. Grafiki kafelkowe mają osobne prawa autorów. Pełne teksty i źródła: `licenses`, `lib/dependencies.json` oraz **`THIRD-PARTY-NOTICES.md`**.
