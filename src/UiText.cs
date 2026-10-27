// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace EftPosition
{
    // Only interface text is translated. Paths, map artwork and data names stay intact.
    public static class UiText
    {
        public static string Language { get; set; }
        static readonly Dictionary<string,string> english=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            {"EFT Position · 13 map","EFT Position · 13 maps"},
            {" · DANE PRZYKŁADOWE"," · SAMPLE DATA"}, {"DANE PRZYKŁADOWE","SAMPLE DATA"},
            {"Questy i filtry","Quests & filters"}, {"Filtry mapy i questy","Map filters and quests"},
            {"Ustawienia","Settings"}, {"Pełny ekran","Full screen"},
            {"Wybór mapy","Select map"}, {"Wybór piętra","Select level"},
            {"MAPA","MAP"}, {"POZIOM","LEVEL"}, {"Cała mapa","Fit map"}, {"Do znacznika","Center player"},
            {"Śledź pozycję","Follow position"}, {"Auto wnętrza","Auto interiors"}, {"Automatyczne wnętrza","Automatic interiors"},
            {"Sterowanie ręczne","Manual control"}, {"Questy na pełnym ekranie","Full screen quests"},
            {"Ustawienia na pełnym ekranie","Full screen settings"}, {"Wyjdź z pełnego ekranu","Exit full screen"},
            {"Wybierz folder w ustawieniach","Choose folder in settings"},
            {"Brak pozycji\nWykonaj zrzut w grze.","No position\nTake a screenshot."},
            {"Kierunek · —","Heading · —"}, {"Kierunek · {0:F1}°","Heading · {0:F1}°"},
            {"Pionowo · brak kierunku","Vertical · no heading"}, {"Wybierz aktualną mapę","Select your current map"},
            {"Folder zrzutów gry","Game screenshot folder"}, {"Ścieżka folderu zrzutów","Screenshot folder path"},
            {"Wybierz folder…","Choose folder…"}, {"Wybierz folder zrzutów","Choose screenshot folder"},
            {"Folder zrzutów zapisanych przez EFT","Folder containing screenshots saved by EFT"},
            {"Obserwuj","Watch folder"}, {"Zatrzymaj","Stop watching"}, {"Usuwaj zrzut po odczycie","Delete screenshot after reading"},
            {"Usuwa rozpoznany PNG po pokazaniu pozycji na mapie.\nPozostałe pliki zostają w folderze. Folder i wybór są\nzapamiętywane na następne uruchomienie.","Deletes a recognized PNG after showing its position.\nOther files stay in the folder. The folder and this choice\nare remembered for the next launch."},
            {"Wygląd, przezroczystość i folder zrzutów (Ctrl+,)","Appearance, transparency and screenshot folder (Ctrl+,)"},
            {"Pełny ekran / powrót · F11","Full screen / return · F11"},
            {"Wysuń questy i ekstrakty tej mapy · Ctrl+F","Open quests and extracts for this map · Ctrl+F"},
            {"Ustawienia · Ctrl+,","Settings · Ctrl+,"}, {"Questy i ekstrakty · Ctrl+F","Quests and extracts · Ctrl+F"},
            {"Powrót do okna · F11 / Esc","Return to window · F11 / Esc"},
            {"Dopasuj piętro i widok po zrzucie. Mapę wybierasz ręcznie.","Adjust level and view after a screenshot. Select the map manually."},
            {"Centruj pozycję po nowym zrzucie.","Center position after a new screenshot."},
            {"Czas ostatniego zrzutu; najedź po odczycie, aby zobaczyć nazwę pliku.","Time of the latest screenshot; hover after reading to see its filename."},
            {"Folder screenshotów zapisanych przez EFT. Możesz wkleić ścieżkę.","Folder for screenshots saved by EFT. You can paste a path."},
            {"Brak zrzutu","No screenshot"}, {"Mapa","Map"}, {"Mapa: ","Map: "}, {"Ostatni zrzut: ","Latest screenshot: "},
            {"Wczytywanie mapy…","Loading map…"},
            {"Ustawienia (Ctrl+,)","Settings (Ctrl+,)"}, {"Questy i ekstrakty (Ctrl+F)","Quests and extracts (Ctrl+F)"},
            {"Pełny ekran / powrót (F11)","Full screen / return (F11)"}, {"Pełna widoczność (F10)","Full visibility (F10)"},
            {"Folder niedostępny. Wybierz go ponownie.","Folder unavailable. Choose it again."},
            {"Poziom bazowy","Base level"}, {"Teren otwarty","Outdoors"}, {"Poza obszarem mapy","Outside map area"},
            {"Rozpoznany budynek\nWidok dopasowany do wnętrza","Building detected\nView adjusted to the interior"},
            {"Wczytywanie mapy; pierwszy raz wymaga internetu…","Loading map; first use requires internet…"},
            {"Nie udało się wczytać mapy.\nSprawdź internet lub pliki programu i wybierz mapę ponownie.\n","Could not load the map.\nCheck your connection or application files and select the map again.\n"},
            {"Nie mogę otworzyć mapy: ","Cannot open map: "}, {"Nieznana mapa: ","Unknown map: "},
            {"Wybierz folder zrzutów.","Choose a screenshot folder."}, {"Oczekiwanie na poprawną nazwę pliku…","Waiting for a valid filename…"},
            {"Oczekiwanie na pozycję\nMapę wybierasz ręcznie.","Waiting for position\nSelect the map manually."},
            {"Sterowanie ręczne\nWybierz poziom z menu.","Manual control\nChoose a level from the menu."},
            {"Nie mogę obserwować folderu","Cannot watch folder"},
            {"Obserwacja wstrzymana.\nOstatnia pozycja zachowana.","Watching paused.\nLast position retained."},
            {"Nie udało się zapisać ustawień. Sprawdź dostęp do folderu programu.","Could not save settings. Check access to the application folder."},
            {"● Obserwacja aktywna","● Watching folder"}, {"Aktualizacje: ","Updates: "}, {"  •  Pominięte nazwy: ","  •  Skipped filenames: "},
            {" s temu"," s ago"}, {" min temu"," min ago"}, {" h temu"," h ago"}, {"Starszy","Older"}, {"Zrzut","Screenshot"},
            {"Wczytywanie mapy…\nPozycja z ","Loading map…\nPosition from "},
            {"Poza obszarem mapy\nSprawdź wybraną lokację.","Outside map area\nCheck the selected location."},
            {"Nie rozpoznano pozycji\nPominięte PNG: ","No position recognized\nSkipped PNGs: "},
            {"Nie udało się usunąć części zrzutów. Pozostały w folderze. Usunięto: ","Some screenshots could not be deleted and remain in the folder. Deleted: "},
            {"Ustawienia · EFT Position","Settings · EFT Position"}, {"Dopasuj widok do siebie","Make the view your own"},
            {"Zmiany widać od razu i zapisują się automatycznie.","Changes apply immediately and are saved automatically."},
            {"Wygląd","Appearance"}, {"Okno i mapa","Window & map"}, {"Zrzuty","Screenshots"},
            {"Ustawienia wyglądu","Appearance settings"}, {"Ustawienia okna i mapy","Window and map settings"}, {"Ustawienia zrzutów","Screenshot settings"},
            {"Motyw kolorystyczny","Color theme"}, {"Tarkov · grafit i piasek","Tarkov · charcoal and sand"},
            {"Noc · chłodny turkus","Night · cool turquoise"}, {"Las · oliwka","Forest · olive"},
            {"Kolor akcentu","Accent color"}, {"Wybierz kolor…","Choose color…"}, {"Wybierz kolor akcentu","Choose accent color"},
            {"Zaokrąglone krawędzie kontrolek i list","Rounded control and list corners"}, {"Zaokrąglone krawędzie","Rounded corners"},
            {"Tło aplikacji","Application background"}, {"Grafika Tarkova","Tarkov artwork"}, {"Własny obraz","Custom image"},
            {"Jednolity kolor","Solid color"}, {"Wybierz obraz…","Choose image…"}, {"Wybierz własne tło","Choose custom background"},
            {"Przyciemnienie tła","Background dimming"}, {"Krycie mapy","Map opacity"}, {"Przywróć wygląd domyślny","Reset appearance"},
            {"Przezroczystość całego okna","Whole window transparency"}, {"Przezroczystość okna","Window transparency"},
            {"Stosuj tylko na pełnym ekranie","Apply only in full screen"}, {"Przezroczystość tylko na pełnym ekranie","Full screen transparency only"},
            {"Odsłania pulpit lub aplikację pod tym oknem.\nMapa i znaczniki również stają się przezroczyste.","Reveals the desktop or application below this window.\nThe map and markers also become transparent."},
            {"Pełna widoczność · F10","Full visibility · F10"}, {"Przywróć pełną widoczność","Restore full visibility"},
            {"Rozmiar znaczników","Marker size"}, {"Pokaż dolny pasek pozycji","Show bottom position bar"}, {"Pokaż pasek pozycji","Show position bar"},
            {"Krycie mapy zmienia tylko rysunek mapy.\nZnaczniki pozostają czytelne na własnym tle.","Map opacity affects only the map artwork.\nMarkers remain readable on their own background."},
            {"Gotowe","Done"}, {"Zamknij ustawienia","Close settings"}, {"JPG / PNG · do 12 MB","JPG / PNG · up to 12 MB"},
            {"Własny obraz zapisany","Custom image saved"}, {"Wybierz tło aplikacji","Choose application background"},
            {"Obrazy JPG i PNG|*.jpg;*.jpeg;*.png|Obrazy BMP|*.bmp","JPG and PNG images|*.jpg;*.jpeg;*.png|BMP images|*.bmp"},
            {"Nie można ustawić tła","Cannot set background"},
            {"Filtry ekstraktów i questów","Extract and quest filters"}, {"Questy i ekstrakty","Quests and extracts"},
            {"Zamknij panel questów","Close quest panel"}, {"Tryb danych znaczników","Marker data mode"},
            {"PvP sezonowy","Seasonal PvP"}, {"TRYB","MODE"}, {"Ekstrakty PMC","PMC extracts"}, {"Ekstrakty Scav","Scav extracts"},
            {"Questy","Quests"}, {"Lokalizacje wybranych questów","Selected quest locations"},
            {"Lokalizacje ekstraktów PMC i wspólnych. Dostępność w rajdzie sprawdź w grze.","PMC and shared extract locations. Check availability in game during the raid."},
            {"Lokalizacje ekstraktów Scav i wspólnych. Dostępność w rajdzie sprawdź w grze.","Scav and shared extract locations. Check availability in game during the raid."},
            {"Pokaż lokalizacje zaznaczonych poniżej questów.","Show locations for the quests selected below."},
            {"Wyczyść wyszukiwanie questów","Clear quest search"}, {"Szukaj questu na wybranej mapie","Search quests on the selected map"},
            {"Szukaj questu na tej mapie","Search quests on this map"}, {"Questy bieżącej mapy","Quests on the current map"},
            {"Zaznacz wyniki","Select results"}, {"Zaznacz widoczne questy","Select visible quests"},
            {"Odznacz mapę","Clear map"}, {"Odznacz questy tej mapy","Deselect quests on this map"},
            {"Brak danych lokalizacji questów\ndla tej mapy.","No quest location data\nfor this map."},
            {"Brak wyników na tej mapie.\nWybrane: ","No results on this map.\nSelected: "},
            {"Wybrane: ","Selected: "}, {" · Punkty: "," · Locations: "}, {"\nNa liście: ","\nListed: "}, {" z "," of "},
            {"⚑ Quest · ↪ PMC / Scav\nSzare questy: inne piętro · tarkov.dev","⚑ Quest · ↪ PMC / Scav\nGray quests: another level · tarkov.dev"},
            {"Mapa: kółko przybliża, przeciągnięcie przesuwa","Map: scroll to zoom, drag to pan"},
            {"Wybierz mapę z menu.","Select a map from the menu."}, {"prawa autorów · cache lokalny","authors' rights · local cache"},
            {"Pasek tytułu okna","Window title bar"}, {"Minimalizuj okno","Minimize window"}, {"Maksymalizuj okno","Maximize window"},
            {"Zamknij okno","Close window"}, {"Przywróć rozmiar okna","Restore window size"},
            {"Wybierz obraz JPG lub PNG do 12 MB.","Choose a JPG or PNG image up to 12 MB."}, {"Wybierz obraz do 20 megapikseli.","Choose an image up to 20 megapixels."},
            {"Nie można otworzyć tła. Wybierz obraz ponownie.","Cannot open the background. Choose the image again."},
            {"Brak danych znaczników. Sprawdź assets/markers.json.","No marker data. Check assets/markers.json."},
            {"Łączenie z folderem…","Connecting to folder…"}, {"Folder nie istnieje.","Folder does not exist."},
            {"Przerwa w obserwacji • ponawiam połączenie…","Watching interrupted • reconnecting…"},
            {"Folder niedostępny • ponawiam…","Folder unavailable • retrying…"},
            {"Brak dostępu do folderu • sprawdź uprawnienia.","Folder access denied • check permissions."},
            {"Folder zniknął • czekam na jego powrót…","Folder missing • waiting for it to return…"},
            {"Nie mogę otworzyć folderu • ponawiam…","Cannot open folder • retrying…"},
            {"Brak dostępu do folderu • ponawiam…","Folder access denied • retrying…"},
            {"Nie mogę obserwować folderu • ponawiam…","Cannot watch folder • retrying…"},
            {"Pobieranie mapy: ","Downloading map: "},
            {"Ścieżka mapy wykracza poza jej folder.","Map path is outside its folder."},
            {"Brakuje obrazu mapy. Rozpakuj całą paczkę.","Map image missing. Extract the entire package."},
            {"Mapa wymaga zbyt wielu kafelków.","Map requires too many tiles."},
            {"Niepoprawny rozmiar kafelka mapy.","Invalid map tile dimensions."},
            {"Kafelek mapy jest zbyt duży.","Map tile is too large."}, {"Źródło nie zwróciło obrazu mapy.","The source did not return a map image."},
            {"Nieznany lub niepełny parametr: ","Unknown or incomplete argument: "},
            {"Nie udało się uruchomić aplikacji.\n","Could not start the application.\n"},
            {"Brak poprawnego spisu map.","No valid map catalog."}, {"Nieprawidłowe poziomy mapy.","Invalid map levels."},
            {"Nieprawidłowa kalibracja mapy: ","Invalid map calibration: "}, {"Niepoprawna macierz mapy.","Invalid map matrix."},
            {"Osobliwa macierz mapy.","Singular map matrix."}, {"Nieprawidłowe źródło mapy.","Invalid map source."},
            {"Nieprawidłowy obraz mapy.","Invalid map image."},
            {"Nieprawidłowa liczba w geometrii SVG.","Invalid number in SVG geometry."},
            {"Nieprawidłowy wykładnik SVG.","Invalid SVG exponent."}, {"Nieprawidłowa współrzędna SVG.","Invalid SVG coordinate."},
            {"Nieprawidłowa flaga łuku SVG.","Invalid SVG arc flag."}, {"Brak polecenia ścieżki SVG.","Missing SVG path command."},
            {"Nieobsługiwane polecenie SVG: ","Unsupported SVG command: "}
        };
        static readonly Dictionary<string,string> polish=Reverse();
        static Dictionary<string,string> Reverse()
        {
            var result=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var pair in english)result[pair.Value]=pair.Key;
            return result;
        }
        public static string T(string text)
        {
            if(String.IsNullOrEmpty(text))return text;
            string value;
            return (Language=="en"?english:polish).TryGetValue(text,out value)?value:text;
        }
        public static string Format(string text,params object[] args)
        {return String.Format(CultureInfo.InvariantCulture,T(text),args);}
        public static string Error(string text)
        {
            string value=T(text);if(value!=text || text==null)return value;
            var dictionary=Language=="en"?english:polish;
            foreach(var pair in dictionary)
                if(pair.Key.EndsWith(": ",StringComparison.Ordinal) && text.StartsWith(pair.Key,StringComparison.Ordinal))
                    return pair.Value+text.Substring(pair.Key.Length);
            return text;
        }
        public static string Lines(string text)
        {
            string translated=T(text);if(translated!=text || text==null)return translated;
            string[] lines=text.Split('\n');for(int i=0;i<lines.Length;i++)lines[i]=T(lines[i]);return String.Join("\n",lines);
        }
        public static void Apply(Control root,ToolTip tips=null)
        {
            // Never translate editable values or the selected quest's data name.
            if(root is Label || root is ButtonBase || root is Form)root.Text=Lines(root.Text);
            root.AccessibleName=T(root.AccessibleName);
            if(tips!=null){string tip=tips.GetToolTip(root);if(!String.IsNullOrEmpty(tip))tips.SetToolTip(root,T(tip));}
            ComboBox combo=root as ComboBox;
            if(combo!=null)
                for(int i=0;i<combo.Items.Count;i++)if(combo.Items[i] is string)
                {string value=T((string)combo.Items[i]);if(value!=(string)combo.Items[i])combo.Items[i]=value;}
            if(root.ContextMenuStrip!=null)ApplyMenu(root.ContextMenuStrip.Items);
            foreach(Control child in root.Controls)Apply(child,tips);
            root.Invalidate();
        }
        static void ApplyMenu(ToolStripItemCollection items)
        {
            foreach(ToolStripItem item in items)
            {item.Text=T(item.Text);var menu=item as ToolStripMenuItem;if(menu!=null)ApplyMenu(menu.DropDownItems);}
        }
    }
}
