# Publikacja 3.2.0

Kod oraz zasoby pochodzą z lokalnie sprawdzonej implementacji 3.2.0. Dołączono licencje map i bibliotek oraz informacje o pochodzeniu nowych teł. Paczki nie zawierają preferencji, prywatnego cache, rzeczywistych zrzutów ani lokalnych raportów ze ścieżkami użytkownika.

`Package-Release.ps1` kopiuje z raportów wyłącznie przejrzany katalog odpowiadający `VERSION`. Pliki EXE z katalogów źródłowych są usuwane przed utworzeniem archiwum kodu. `BUILD-INFO.json` w paczce Windows wskazuje hash programu i rewizję źródeł.

Zmiana `VERSION` na `main` uruchamia zatwierdzony workflow: budowanie, testy, pakowanie i nowe wydanie testowe. Istniejące wydania nie są nadpisywane. Do dotychczasowych kontroli dodano języki/podpowiedzi oraz raster. Workflow korzysta z krótkotrwałego tokenu GitHub Actions z prawem zapisu zawartości, bez osobistego tokenu w repozytorium.

Wyniki dawnych pomiarów wydajności i pamięci nie są przypisywane binarnemu plikowi utworzonemu na GitHubie. Nowe paczki i ich sumy powstają w tym samym przebiegu automatyzacji.
