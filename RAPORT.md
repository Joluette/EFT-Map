# EFT Position 3 — raport z 26.10.2026

## Co działa

- Ostre mapy: 30 poziomów SVG jest rysowanych w aktualnej skali do bufora wielkości widoku. Powiększanie nie rozciąga starego obrazu całej mapy.
- Dokładniejsze kafelki map rastrowych są dobierane do przybliżenia i widocznego fragmentu, z ograniczeniem cache do 24 MiB rozkodowanych obrazów.
- „Auto wnętrza” po nowym SS rozpoznaje piętro z X/Y/Z, dopasowuje obszar budynku lub podziemi, a po wyjściu przywraca widok ogólny. Tolerancja granic ogranicza przeskakiwanie przy schodach; ten sam kontekst nie wymusza zbliżenia przy każdym zrzucie.
- Pełny ekran: przycisk lub F11; powrót przez Esc/F11 albo menu pod prawym przyciskiem. W pełnym ekranie mapa zajmuje całe okno bez paneli i ramki, a obserwacja zrzutów działa dalej.
- Pozostaje ręczny wybór spośród 13 map. Katalog ma 55 poziomów, w tym dodatkowe piętra Customs i Reserve z kafelków źródłowych.

To nadal jedno natywne okno WinForms, bez przeglądarki, WPF i procesów pomocniczych. Aplikacja nie czyta pamięci/logów gry, nie otwiera obrazów screenshotów, nie wysyła klawiszy i nie zmienia zrzutów.

## Naprawione błędy zgłoszone podczas pracy

**Długie linie wychodzące z budynków:** starszy renderer źle interpretował względne rozpoczęcie kolejnego podobszaru po zamknięciu ścieżki SVG. Polecenia są teraz zamieniane na równoważne bezwzględne polecenia przed rysowaniem. Zachowuje to geometrię i kalibrację, zamiast ukrywać linie. Osobny test kontroluje obszar, w którym występowały błędne przekątne.

**Brakujące drogi, rzeka i drzewa:** naprawiono dziedziczenie stylów CSS przez przepisywanie deklaracji na oryginalny element. W renderze kontrolnym Customs wykryto 12 964 piksele koloru rzeki, 18 018 drzew i 13 913 budynków; niepożądanych pikseli przy początku mapy jest **0**.

**Okno błędu MapTest.exe:** dotyczyło roboczego pomocnika, który podawał względną ścieżkę do biblioteki SVG. Wymuszono pełną ścieżkę i dodano przechwytywanie wyjątków do testów. MapTest.exe nie jest aplikacją produkcyjną ani składnikiem paczki. Po poprawce nie pozostały jego procesy ani okna błędu.

**Restart obserwacji po przybliżeniu wnętrza:** nowa sesja folderu przywraca widok ogólny, aby stary zoom nie ukrywał nowej pozycji na otwartym terenie. Ten przypadek ma test regresji.

## Środowisko i badany plik

Windows 10 x64 `10.0.19045`, .NET Framework 4.8 (`4.8.09037`, Release 533325), systemowy kompilator `Framework64\v4.0.30319\csc.exe`. Biblioteki natywnego renderowania: SharpVectors 1.8.5, BSD-3-Clause, dokładnie pięć DLL z manifestem SHA-256 w `lib`.

SHA-256 finalnego, zmierzonego EXE:

```text
670BA750E157E85551FEF4E1C4FD91FFD015D47D9B62E8E138B70BAFF1CF9598
```

Ponowna kompilacja może zmienić hash ze względu na znacznik czasu kompilatora. Program w wydaniu Windows odpowiada mierzonej wersji. Repozytorium oraz osobny ZIP kodu zawierają źródła i narzędzia budowania; plik EXE znajduje się w ZIP aplikacji.

## Wykonane testy

| Zestaw | Wynik | Główne sprawdzane zachowanie |
|---|---:|---|
| Rdzeń i katalog | **5859 — PASS** | Format nazw, kultury liczb, złe dane, kwaterniony, kalibracja 13 map/55 poziomów, obserwacja folderu, seria 2000 plików |
| Automatyczna nawigacja | **47 — PASS** | Dorms, piętra, nakładanie zakresów i histereza, ZB-013, Factory, Labs, Icebreaker, Interchange, Reserve |
| Kafelki offline | **107 — PASS** | Geometria, Labs 175/256 px, ujemne początki, kolejka ≤64, cache ≤24 MiB i ≤96 wpisów, zwalnianie zasobów |
| Kafelki z serwera | **161 — PASS** | Rzeczywiste detale Labs z6, Labyrinth z6, Icebreaker z5, Customs 4th Floor z6, Reserve 2nd Floor z6 |
| Renderer SVG | **100 — PASS** | Wszystkie 30 SVG, ostre ponowne rysowanie po zoomie, cache widoku, względne podścieżki, style, brak błędnych linii |
| Dotychczasowe GUI | **85 — PASS** | Menu map i poziomów, przybliżanie/przesuwanie, centrowanie, szybkie zmiany wyboru, zatrzymanie obserwacji |
| Nowe funkcje GUI | **94 — PASS** | Automatyczne wnętrza i piętra, brak ponownego snapowania, ręczny tryb, ostrość, pełny ekran/Esc/F11, przywrócenie okna, restart obserwacji |

W testach GUI działała rzeczywista pętla komunikatów WinForms. Zdarzenia kontrolek były wywoływane wewnątrz programu testowego; nie generowano wejścia do Windows ani EFT. Obejrzano render zwykłego widoku, dużego przybliżenia wnętrza i pełnego ekranu 1920×1080. Test szczegółów sprawdza treść samej mapy poza znacznikiem i interfejsem.

Pozycje były syntetyczne. Wszystkie utworzone PNG pozostawiono do obejrzenia. Zrzutów użytkownika ani plików gry nie usuwano. Grafiki pobrane do QA pozostały w osobnym katalogu roboczym poza dystrybuowanym ZIP.

Sprawdzono także **wszystkie 55 rzeczywistych obrazów poziomów — PASS**, ich rozmiary i obecność treści. Surowe wyniki są w `reports`: `tests.txt`, `navigation-tests.txt`, `tile-tests.txt`, `hires-network-tests.txt`, `vector-tests.txt`, `gui-tests.txt`, `experience-tests.txt`, `all-level-assets.txt`.

## Pomiar RAM finalnej wersji

Finalny przebieg zakończono **26.10.2026 o 23:47 czasu Europe/Warsaw**. Trwał **120,15 s** i obejmował pełny ekran przez cały przebieg, **12 000 poprawnych nazw**, **120 błędnych nazw**, wszystkie 13 map oraz **90 ukończonych wczytań**.

Metoda:

1. Po 15 s rozgrzewki dopisywano pliki przez około 75 s, a końcowe 30 s nie dodawało nowych plików.
2. Widok przybliżał się, przesuwał i dopasowywał co około 100 ms. Mapy zmieniały się co około 1,5 s. Automatyka wnętrz była włączona.
3. Zewnętrzny skrypt próbkował `Process.WorkingSet64` i `PrivateMemorySize64` co około 250 ms, sumując aplikację i procesy potomne. Drzewo procesów sprawdzał przez `Win32_Process` co około 2 s.
4. Aplikacja co około 1 s zapisywała dodatkowo własne liczniki oraz systemowy `PeakWorkingSet64`, aktywną mapę, tryb pełnoekranowy i stan ostatniego zrzutu.
5. Nie wymuszano GC, nie przycinano working setu ani nie minimalizowano okna dla uzyskania niższego wyniku. Obrazy bazowe trzech pobieranych map były wcześniej w cache; dostępność szczegółowych kafelków sprawdzono osobnym testem sieciowym.

| Licznik | Wynik |
|---|---:|
| Szczyt working set zapisany przez system | **158 199 808 B = 158,20 MB = 150,87 MiB** |
| Najwyższa próbkowana suma working set drzewa | 148 799 488 B = 148,80 MB |
| Najwyższa próbkowana suma Private Bytes drzewa | **128 016 384 B = 128,02 MB** |
| Working set na początku zapisu wewnętrznego | 62 091 264 B = 62,09 MB |
| Working set w końcowej próbce | **102 932 480 B = 102,93 MB** |
| Private Bytes w końcowej próbce | 81 928 192 B = 81,93 MB |
| Procesy w drzewie | **1**, bez wykrytych procesów pomocniczych |
| Mapy / ukończone wczytania | **13 / 90** |
| Tryb pełnoekranowy w próbkach | **Włączony** |
| Cache powtórzeń | **256 wpisów** |
| Pozostawione PNG | **12 120** |
| Ostatnia pozycja / plik | **Poprawne: X=-1,00; Z=59,00; sufiks (11999)** |
| Wyjście aplikacji | **0 — bez błędu** |

Limit potraktowano jako **400 000 000 B**, nie 400 MiB. **Został spełniony w tym przebiegu.** Working set oznacza pamięć rezydującą, Private Bytes prywatną pamięć zadeklarowaną przez proces. Szczyt systemowy jest wyższy od próbkowanego, bo krótki szczyt może wypaść pomiędzy próbkami; podajemy wartość konserwatywną.

Skrypt pomiarowy jest zewnętrznym narzędziem QA, nie wymaganym procesem pomocniczym aplikacji. CLR, renderer, obserwator i pobieranie działają w tym samym procesie. Odczyt drzewa wymagał dodatkowych uprawnień środowiska; zwykły program nie wymaga administratora. Próbkowanie co 2 s może przeoczyć bardzo krótki proces, więc sprawdzono również źródła: aplikacja nie uruchamia innych programów.

1368 zaakceptowanych aktualizacji oznacza scalanie powtórzeń i pomijanie starszych kandydatur, a nie brak końcowej pozycji. Stan końcowy porównano z dokładną nazwą ostatniego pliku. Pełne liczniki: `memory-summary.json`, `memory-app.csv`, `memory-process-tree.csv`. Narzędzie do powtórzenia: `tools/Measure-Memory.ps1 -CycleMaps -FullScreen`.

## Ograniczenia

**EFT nie uruchamiano.** Nie deklarujemy pomiaru dokładności na żywym kliencie ani zatwierdzenia przez wydawcę. Automat opiera się na społecznościowych wysokościach i obszarach; zastępczy prostokąt obrysu budynku jest heurystyką, nie kolizją gry. Może pomylić się przy nieregularnych budynkach lub niepełnych metadanych. Można go wyłączyć i wybrać piętro ręcznie.

Nie ma identyfikatora mapy w nazwie PNG, więc lokacja pozostaje wybierana ręcznie. Aktualizacje następują po nowym zrzucie, nie pomiędzy nimi. Na mapach kafelkowych maksymalną ostrość ogranicza rozdzielczość udostępniona przez źródło.

Pomiar trwał dwie minuty na jednej maszynie. Nie jest wielogodzinnym testem wycieków ani gwarancją wyniku przy dowolnym rozmiarze monitora. Większy widok potrzebuje większych buforów. Licencje map i biblioteki zachowano w `THIRD-PARTY-NOTICES.md` i `licenses`.
