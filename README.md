# EFT Position 3.1.0

Przenośna aplikacja na Windows x64 pokazująca pozycję i kierunek z ostatniego zrzutu Escape from Tarkov. C# / WinForms / .NET Framework 4.8, jeden proces. Nie czyta pamięci ani logów gry i nie symuluje klawiszy.

## Pobierz i uruchom

1. Otwórz [wydanie 3.1.0](https://github.com/Joluette/EFT-Map/releases/tag/3.1.0).
2. W **Assets** pobierz **EFT-Position-Windows-3.1.0.zip** i rozpakuj cały folder.
3. Uruchom **EftPosition.exe** lub **Uruchom.cmd**. Zachowaj `assets`, `lib` i `EftPosition.exe.config` obok programu.
4. Wybierz mapę i folder zrzutów zapisywanych przez EFT, zwykle `Dokumenty\Escape from Tarkov\Screenshots`.
5. Zrób zrzut funkcją gry. Znacznik pokazuje pozycję z tego zrzutu; nowy zrzut aktualizuje pozycję.

Folder jest zapamiętywany, a obserwacja wraca przy następnym uruchomieniu. **Przyklad.cmd** pokazuje próbkę bez uruchamiania EFT. Gotowa paczka nie wymaga Visual Studio, Node ani Pythona. Wydanie pozostaje prototypem; [3.0.0](https://github.com/Joluette/EFT-Map/releases/tag/3.0.0) jest zachowane.

## Zmiany w 3.1.0

- Płynny zoom i płynniejsze przesuwanie SVG, odświeżony interfejs.
- Ekstrakty PMC/Scav, lokalizacje questów, filtry oraz wybór jednego lub kilku questów.
- Wyszukiwanie po polsku/angielsku w obrębie bieżącej mapy i trybu (PvP/PvE/PvP sezonowy). Wielkość liter i polskie znaki nie są wymagane. Zmiana zapytania zachowuje zaznaczenia.
- Złote pinezki z flagą dla questów oraz zielone/niebieskie plakietki wyjść. Po najechaniu widać nazwę. Questy na innym rozpoznanym piętrze są przyciemnione.
- Zapamiętywanie folderu i opcji usuwania zrzutów; odczyt nazw z dodatkowym polem np. `_16.60`.
- Nowa ikona i metadane wersji aplikacji.

![Questy i ekstrakty na Ground Zero, dane przykładowe](reports/preview-3.1.0.png)

## Zrzuty i ustawienia

**Usuwaj zrzut po odczycie** jest domyślnie włączone. Rozpoznany PNG jest usuwany po pokazaniu poprawnej pozycji na wczytanej mapie. Nierozpoznane pliki, niepokazane zrzuty i pozycje poza obszarem wybranej mapy pozostają w folderze; program nie czyści całego archiwum. Ostatnia pozycja pozostaje w pamięci. Możesz wyłączyć usuwanie w panelu zrzutów; wybór jest zapamiętywany. Tryb przykładowy (`--demo`) zachowuje pliki.

Jeśli gra nadal zapisuje plik, usuwanie jest ponawiane do 15 s. Kontrola metadanych i wyłączny uchwyt chronią przed usunięciem pliku zmienionego po odczycie. Niedostępny lub zmieniony plik pozostaje w folderze. Zawartość obrazu nie jest odczytywana — źródłem pozycji jest nazwa i metadane pliku.

Ustawienia są lokalnie w `cache/app-preferences.json`, a zaznaczenia questów w `cache/marker-preferences.json`. Wybory są rozdzielone według mapy i trybu. Paczki nie zawierają prywatnego cache ani ustawień. Rozpakuj program do folderu z prawem zapisu, np. do Dokumentów.

## Obsługa mapy

- Kółko i **+ / −**: zoom. Przeciągnięcie: przesuwanie.
- **Cała mapa**, **Do znacznika**, opcjonalne **Śledź pozycję**.
- **Auto wnętrza** dobiera poziom i zbliżenie z X/Y/Z nowego zrzutu. Mapę wybierasz ręcznie; piętro można też zmienić ręcznie.
- **Filtry** lub **Ctrl+F**: questy i ekstrakty. **Pozycja** wraca do ostatniego zrzutu.
- **F11**: pełny ekran. **Esc / F11**: powrót. Menu pod prawym przyciskiem zawiera dodatkowe sterowanie i czas zrzutu.

13 map i 55 poziomów. Część map i poziomów wymaga internetu przy pierwszym pobraniu, później korzysta z cache. Znaczniki pokazują lokalizacje; dostępność ekstraktu sprawdzasz w grze. Questy działają offline i obejmują cele z opublikowanymi współrzędnymi. Terminal nie ma punktów w dołączonej migawce. Dla obszaru marker wskazuje punkt źródłowy, a dla przedmiotu możliwe miejsca pojawienia się. Bez wysokości lub rozpoznanego poziomu celu znacznik nie jest przyciemniany.

## Kod i testy

Kod pobierzesz z repozytorium lub **EFT-Map-kod-zrodlowy-3.1.0.zip**. Kliknij **Zbuduj.cmd**, aby użyć systemowego kompilatora .NET Framework 4.8. **Testuj.cmd** uruchamia testy; część wymaga zwykłej sesji pulpitu Windows. Biblioteki są w `lib`; budowanie nie pobiera NuGet.

`src` zawiera kod, `tests` kontrole, `assets` mapy i lokalizacje, a `tools` narzędzia przygotowania danych. `tools/Update-Markers.ps1` odświeża migawkę tarkov.dev (Node i internet potrzebne tylko podczas przygotowania). `tools/Package-Release.ps1` tworzy czyste paczki programu i źródeł. Zmiana `VERSION` na `main` uruchamia sprawdzenie i publikację nowego wydania przez GitHub Actions.

Wyniki i metoda pomiaru pamięci: [RAPORT.md](RAPORT.md), zmiany: [RELEASE-NOTES.md](RELEASE-NOTES.md). Paczki mają sumy SHA-256. Szczyt 148,32 MB dotyczy opisanej dwuminutowej próby implementacji; nie jest gwarancją każdej konfiguracji lub długiej sesji.

## Źródła i licencje

Kod: **GPL-3.0-only**. Kalibracja i dane map tarkov.dev: **MIT**. Mapy Shebuki: **CC BY-NC-SA 4.0**, użycie niekomercyjne z atrybucją. SharpVectors 1.8.5: **BSD-3-Clause** wraz z informacjami o składnikach. Lokalizacje questów i ekstraktów: publiczne [JSON API tarkov.dev](https://tarkov.dev/api); adresy i sumy migawki w `assets/markers.source.json`. Pobierane kafelki mają osobne prawa autorów i nie są dołączone jako cache.

Pełne informacje: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), `licenses` i `lib/dependencies.json`.
