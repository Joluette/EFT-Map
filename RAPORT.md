# Weryfikacja wydania 3.2.0

27.10.2026. Windows x64 / .NET Framework 4.8. Testy uruchomiono na osobnych danych i cache, bez uruchamiania EFT i bez korzystania z rzeczywistych zrzutów użytkownika.

## Testy

Pełny przebieg lokalny: **13 zestawów, 11 611 sprawdzeń — PASS**. Lokalny EXE miał SHA-256 `8E318834C35F104908CF48B4BD751F04C159FAF8B996213C5E78746141CC14E2`.

Pełne wyniki bieżącego uruchomienia są w `reports/3.2.0/tests.txt`. Obejmują parser, obserwację folderu, nawigację, kafelki, wektory, interfejs, markery, czyszczenie zrzutów, ustawienia, pasek tytułu, raster, języki oraz podpowiedzi.

Testy PL/EN sprawdzają zmianę w obie strony bez restartu, zapis i ponowne uruchomienie, menu ustawień, podpowiedzi, zachowanie filtrów, trybu danych, questów i wyglądu. Nazwy ekstraktów sprawdzono przy rozmiarach 80%, 100% i 150%, nakładających się punktach oraz zmianie kamery przed odrysowaniem. Osobno sprawdzono zdarzenie otwarcia natywnej podpowiedzi.

Testy rastra porównują bufor widoku z pełnym odrysowaniem. Obejmują przezroczystość, przesuwanie, piramidę pomniejszeń, nowe kafelki, zmianę rozmiaru, przywracanie jakości po ruchu i zwalnianie zasobów. Testy paska tytułu obejmują przesuwanie, min/max, powrót, pełny ekran i motyw.

Test GUI pomija niepobrane mapy Icebreaker, Labs i Labyrinth w swoim osobnym cache offline. Testy rastra korzystają z własnych obrazów i kafelków. Podglądy są obrazami odrysowanych kontrolek WinForms; nie mierzą prezentacji na fizycznym ekranie ani FPS gry.

## Wydajność i pamięć — wcześniejszy pomiar implementacji

Przed dodaniem PL/EN przetestowano raster na lokalnym EXE `B221515DB76EB43FDCBD22F0E9E3C1B6E8E2004CCFDF1912BB5477BEB38404CA`. W kontrolce 1920×1080 przy zbliżeniu 6×, mapie 40% i syntetycznych detalach z dysku średni czas synchronicznego odrysowania podczas przesuwania wyniósł około 21–22 ms zamiast 172–184 ms, a podczas animowanego zoomu około 46–47 ms zamiast 160–167 ms. Jednorazowe odrysowanie w docelowej jakości po ruchu trwało w tym ciężkim scenariuszu około 131–146 ms.

Minutowa próba tego samego wcześniejszego EXE, obejmująca 13 map i 40 wczytań, wykazała systemowy szczyt working set 165 117 952 B, jeden proces i brak potomków. To pomiar jednego komputera, z wcześniej pobranymi mapami. Nie jest gwarancją FPS, zużycia RAM ani działania przez wiele godzin.

Nie powtarzano tego pomiaru dla języków ani pliku zbudowanego przez GitHub Actions. Dane te opisują wcześniejszy etap implementacji. Bieżący plik CI ma własny hash zapisany w `BUILD-INFO.json` w archiwum Windows.

## Publikacja

GitHub Actions buduje z przypiętej rewizji i sprawdza rdzeń, nawigację, kafelki, ustawienia/usuwanie, płynny widok, języki/podpowiedzi oraz raster. Po ich przejściu tworzy dwa archiwa i `SHA256SUMS.txt`. Lokalny `Testuj.cmd` uruchamia również pełne zestawy GUI, markerów, wyglądu i paska tytułu.

W paczkach są jedynie raporty przejrzane do publikacji. Pochodzenie map, danych, bibliotek i teł opisano w `THIRD-PARTY-NOTICES.md`.
