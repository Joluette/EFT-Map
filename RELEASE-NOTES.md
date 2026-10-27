# EFT Position 3.1.0

Aktualizacja prototypu na Windows x64 / .NET Framework 4.8.

- Płynniejsze przybliżanie i przesuwanie map SVG oraz odświeżony interfejs.
- Znaczniki ekstraktów i miejsc questowych. Filtry PMC, Scav i Questy.
- Wyszukiwanie po polsku i angielsku wyłącznie w questach bieżącej mapy i trybu (PvP, PvE, PvP sezonowy). Można wybrać kilka questów; wybór jest zapamiętywany.
- Nowe pinezki i plakietki. Questy na innym rozpoznanym piętrze są przyciemnione; podpowiedź zawiera nazwę questu.
- Zapamiętywanie folderu screenshotów i automatyczna obserwacja po ponownym uruchomieniu.
- Opcja **Usuwaj zrzut po odczycie**, domyślnie włączona. Usuwa rozpoznany PNG dopiero po pokazaniu pozycji na wczytanej mapie. Inne pliki pozostają. Opcję można wyłączyć; tryb przykładowy zachowuje pliki.
- Poprawiony odczyt nazw screenshotów z dodatkowym polem liczbowym po obrocie, np. `_16.60`.
- Nowa ikona aplikacji i informacje o wersji w pliku EXE.

## Pobieranie

Pobierz **EFT-Position-Windows-3.1.0.zip**, rozpakuj cały folder i uruchom **EftPosition.exe** lub **Uruchom.cmd**. Zachowaj `assets`, `lib` i `EftPosition.exe.config`. Do działania nie potrzeba Visual Studio, Node ani Pythona. **Przyklad.cmd** pokazuje próbkę bez uruchamiania EFT.

**EFT-Map-kod-zrodlowy-3.1.0.zip** zawiera kod i narzędzia do samodzielnego budowania przez **Zbuduj.cmd**. Integralność archiwów sprawdzisz w **SHA256SUMS.txt**. Wydanie 3.0.0 pozostaje dostępne.

## Zakres i sprawdzenie

13 map; lokację wybierasz ręcznie. Pozycja pochodzi z ostatniego zrzutu. Program nie czyta pamięci ani logów gry i nie symuluje klawiszy. Część map/poziomów wymaga internetu przy pierwszym pobraniu. Dane questów i ekstraktów działają offline; dostępność wyjścia w rajdzie sprawdzasz w grze. Terminal nie ma punktów w dołączonej migawce danych. Brak znanej wysokości celu nie powoduje przyciemnienia.

Lokalne kontrole: 5922 rdzeń/obserwacja, 47 nawigacja, 4173 znaczniki, 42 ustawienia/usuwanie/piętra, 137 GUI, 111 wnętrza/pełny ekran i 27 płynność — PASS. Pomiar implementacji przed dodaniem metadanych wydania: 120,26 s, 13 map, 86 wczytań, szczyt working set 148,32 MB, jeden proces. Szczegóły i ograniczenia w **RAPORT.md**; nie jest to gwarancja zużycia RAM w każdej sesji. Testy korzystały z oddzielnych danych przykładowych bez uruchamiania EFT.

Źródła i licencje zachowano w **THIRD-PARTY-NOTICES.md**, `licenses` i `lib/dependencies.json`.
