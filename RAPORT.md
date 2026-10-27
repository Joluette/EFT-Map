# Weryfikacja implementacji wydania 3.1.0

27.10.2026. Windows x64 / .NET Framework 4.8. Oddzielne syntetyczne pliki, bez uruchamiania EFT. Pliki użytkownika nie są częścią raportu.

| Zestaw | Wynik |
|---|---:|
| Parser, katalog, przeliczenia, obserwacja | 5922 PASS |
| Automatyczna nawigacja i piętra | 47 PASS |
| Znaczniki, wyszukiwanie i wybór wielu questów | 4173 PASS |
| Folder, usuwanie, blokada, ponowienie, piętra, start, ikona | 42 PASS |
| Interfejs | 137 PASS |
| Wnętrza i pełny ekran | 111 PASS |
| Płynny zoom i przesuwanie | 27 PASS |

Test interfejsu pomijał trzy niepobrane obrazy internetowe w osobnym cache. Pomiar poniżej obejmował wszystkie 13 map z przygotowanego cache. Wcześniejszy audyt danych: 3486 zgodności z rekordami źródłowymi. W etapie usuwania nie zmieniano współrzędnych questów.

## Pamięć

Rzeczywisty proces WinForms, 120,26 s, pełny ekran, wszystkie dostępne questy i warstwy, 13 map / 86 ukończonych wczytań, zoom i przesuwanie, 2000 poprawnych i 20 błędnych nazw PNG.

- Systemowy szczyt working set: **148 316 160 B = 148,32 MB**.
- Próbkowany szczyt working set drzewa: **137 162 752 B**.
- Próbkowany szczyt Private Bytes: **121 516 032 B**.
- Końcowy working set: **81 113 088 B**.
- Jeden proces, bez wykrytych potomków, poprawna końcowa pozycja, wyjście 0.
- Limit 400 000 000 B spełniono w tym przebiegu.

Próbkowanie zewnętrzne około 250 ms, inwentaryzacja potomków przez CIM około 2 s, liczniki procesu i szczyt systemowy co 1 s. Bez wymuszania GC i przycinania working set. Pomiar używał `--demo`; usuwanie sprawdza osobny test normalnego trybu.

Zmierzony EXE przed dodaniem numeru wydania miał SHA-256 `1EEE4A03DDF3AA9FC5B8B1D010FC4EDFF7B0B58F9A94CAB2F90C0BB91A7F19BE`. Wydanie buduje się z tej implementacji z metadanymi wersji 3.1.0. Kompilacja na GitHubie daje osobny plik EXE; jego hash i rewizja źródeł są w `BUILD-INFO.json` paczki. Pomiar dotyczy lokalnego pliku wskazanego hashem; nie jest pomiarem identyczności binarnej CI ani wielogodzinnej sesji.

Wyniki: `reports/3.1.0`. Zanonimizowano ścieżki fixture, zachowując liczby. Podgląd pokazuje dane przykładowe z panelem questów. Starsze raporty w repozytorium dokumentują wersję 3.0.0.

GitHub Actions ponownie kompiluje kod i sprawdza parser/obserwację, nawigację, kafelki, ustawienia/usuwanie/piętra oraz płynny widok. Paczki powstają po przejściu kontroli. Lokalny `Testuj.cmd` zawiera też pełne kontrole GUI i markerów.
