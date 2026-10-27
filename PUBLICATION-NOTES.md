# Publikacja 3.1.0

Kod, biblioteki, mapy i lokalizacje są dołączone z licencjami i atrybucją. Nie dołączono ustawień folderu, osobistego cache, rzeczywistych screenshotów, surowej diagnostyki użytkownika ani obrazów z jego ścieżką. W wybranych raportach ścieżki fixture zastąpiono ogólnymi nazwami, zachowując liczby pomiarów.

Zmiana VERSION na main uruchamia release.yml: budowanie i testy Windows, paczki programu i kodu oraz nowe wydanie prototypowe z sumami SHA-256. Istniejące wydania nie są nadpisywane. Workflow używa krótkotrwałego tokenu własnego zadania GitHub Actions; repozytorium nie przechowuje prywatnego klucza ani osobistego tokenu.

BUILD-INFO.json w paczce Windows zawiera hash EXE i rewizję źródeł. Raport pamięci wskazuje osobno hash wcześniej zmierzonej implementacji. Metadane wersji nie zmieniają działania programu. Starsze publiczne raporty pozostają w historii repozytorium.
