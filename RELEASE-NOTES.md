# EFT Position 3.2.0

Nowy wygląd, wybór polskiego lub angielskiego i płynniejsze duże mapy. Wydanie testowe dla Windows x64 / .NET Framework 4.8.

- **Polski i angielski:** Ustawienia → Wygląd → Język / Language. Zmiana działa od razu i zostaje zapisana na następne uruchomienie.
- **Nazwy ekstraktów po najechaniu:** zatrzymaj kursor nad ikoną PMC lub Scav, aby zobaczyć nazwę. Nakładające się punkty pokazują wszystkie nazwy.
- **Więcej miejsca na mapę:** kompaktowy nagłówek, wysuwany panel questów, oddzielne okno ustawień i małe przyciski w pełnym ekranie.
- **Personalizacja:** motywy Tarkov, Noc i Las, własny akcent, tło z pliku lub dołączona grafika, przyciemnienie, krycie mapy i całego okna, zaokrąglone krawędzie, rozmiar znaczników i opcjonalny pasek pozycji.
- **Pasek tytułu zgodny z motywem**, z zachowaniem przesuwania, zmiany rozmiaru, maksymalizacji i powrotu z pełnego ekranu.
- **Płynniejsze przesuwanie i przybliżanie PNG**, w tym Labs, Labiryntu i Icebreakera. Podczas ruchu działa szybki podgląd, po zatrzymaniu wraca docelowa jakość. Wspólny bufor widoku obsługuje też SVG i przezroczystość.
- Zachowane funkcje 3.1: 13 map i 55 poziomów, automatyczne wnętrza, ekstrakty, wybór wielu questów, wyszukiwanie po polsku i angielsku, zapamiętywanie folderu oraz opcjonalne usuwanie rozpoznanego zrzutu po pokazaniu pozycji.

## Pobieranie i aktualizacja

Pobierz **EFT-Position-Windows-3.2.0.zip**, rozpakuj cały folder i uruchom **EftPosition.exe** lub **Uruchom.cmd**. Zachowaj `assets`, `lib` i `EftPosition.exe.config`. Do uruchomienia nie potrzeba Visual Studio, Node ani Pythona. **Przyklad.cmd** pokazuje dane przykładowe bez uruchamiania EFT.

Przy aktualizacji zamknij aplikację i zachowaj swój folder `cache`: zawiera ustawienia, wybrane questy i pobrane mapy. Paczka wydania nie zawiera osobistego cache. Starsze preferencje są wczytywane z polskim interfejsem, bez zmiany folderu i opcji zrzutów.

**EFT-Map-kod-zrodlowy-3.2.0.zip** zawiera kod oraz narzędzia budowania. Sumy obu archiwów są w **SHA256SUMS.txt**. Wcześniejsze wydania pozostają dostępne.

## Skróty

- **Ctrl+,** — ustawienia.
- **Ctrl+F** — questy i filtry.
- **F11** — pełny ekran / powrót; **Esc** — powrót z pełnego ekranu.
- **F10** — przywrócenie pełnej widoczności okna.

## Zakres i sprawdzenie

Pozycja pochodzi z nazwy ostatniego zrzutu zapisanego przez EFT. Mapę wybierasz ręcznie. Aplikacja nie śledzi ruchu pomiędzy zrzutami, nie czyta pamięci ani logów gry. Część map wymaga internetu przy pierwszym wczytaniu. Questy i ekstrakty korzystają z dołączonych danych; dostępność wyjścia w rajdzie sprawdzaj w grze.

Pełny lokalny zestaw testów wydania opisano w **RAPORT.md**. Testy korzystają z osobnych folderów i cache. GitHub Actions buduje własny EXE, przeprowadza kontrole i dopiero potem tworzy paczki oraz wydanie. Jego hash i rewizja są w `BUILD-INFO.json`.

Tła Tarkov i Tarkov × Cyberpunk (HD) są rekonstrukcjami wygenerowanymi z użyciem AI na podstawie referencji. Nie są oficjalnymi materiałami ani potwierdzoną współpracą marek. Źródła i prawa do grafik oraz map opisano w **THIRD-PARTY-NOTICES.md**.

## English

The interface is now available in Polish and English. Open **Settings → Appearance → Język / Language → English (EN)**. The selection applies immediately and is remembered. Hover a PMC or Scav extract icon to see its name.

This release also adds the compact interface, appearance settings, a themed title bar and smoother PNG map navigation. Download **EFT-Position-Windows-3.2.0.zip**, extract the entire folder and run **EftPosition.exe**. Keep your existing `cache` folder when updating. Position updates after each new screenshot saved by the game.
