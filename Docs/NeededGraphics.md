# NeededGraphics

Co brakuje graficznie, i co proponuję żeby zakładki żyły. Krótko, jedna lista, aktualizowana na bieżąco.

Zasada: **wszystko poniżej ma działający fallback** — gra nie wywala się bez tych plików, tylko wygląda ubożej. Nic tu nie jest blokerem.

---

## 1. Brakujące pliki (posortowane po tym, ile dają)

| # | Plik | Gdzie | Co jest teraz zamiast | Rozmiar |
|---|------|-------|----------------------|---------|
| 1 | `Resources/Ui/office_decorate.png` | Trzeci przycisk w prawym pasku SITE | napis „DECOR" | 128×128, PNG, przezroczyste tło |
| 2 | `Resources/Ui/hire.png` | Przycisk HIRE NOW w TEAM | sam tekst | 128×128 |
| 3 | `Resources/Ui/hire_remote.png` | Przycisk HIRE NOW – REMOTE | sam tekst | 128×128 |
| 4 | `Resources/Hiring/ithand_logo.png` | Pasek adresu IThand.hck | tylko tekst adresu | 256×64, cyjan |
| 5 | `Resources/Hiring/getadmin_logo.png` | Pasek adresu get-admin.hck | tylko tekst adresu | 256×64, złoto na czerni |
| 6 | `Resources/Hiring/register_crest.png` | Nagłówek Agencji Pracy | tylko tekst | 96×96, godło urzędu, beż/szarość |
| 7 | `Resources/Character/Looks/look_09..look_14` | Portrety kandydatów | 9 istniejących twarzy się powtarza | prefaby humanoidalne |
| 8 | **Kobiece postacie** | Portrety, biuro | żadna z dwóch paczek ich nie ma | najpilniejsze z całej listy |
| 9 | `Resources/Furniture/*.png` (10 szt.) | Sklep z meblami | kolorowy kwadrat (swatch) | 96×96, po jednej na `FurnitureKind` |
| 10 | Zdjęcia podzespołów | COMPUTE (wybór krzemu) | — | patrz `ART_TODO.md`, wciąż otwarte |
| 11 | `Resources/Models/type_*.png` (5 szt.) | Tabela modeli w zakładce MODEL | kolorowy kafelek z literą | 96×96, po jednej na `ModelType` |
| 12 | `Resources/Labs/lab_huggyface.png` | Kafelek HuggyFace w YOUR LAB | litery „HF" | pozostałe 3 kafelki mają już prawdziwe logo |

### Uwaga do #7 i #8

Kandydaci losują twarz z `PortraitSeed % LookCount`. Przy 9 wyglądach i 6 kandydatach na liście **powtórki są widoczne od razu**. Do 14 wyglądów problem praktycznie znika. Brak kobiet w obu paczkach to nie jest kwestia estetyki — to firma AI złożona wyłącznie z mężczyzn, czego nikt świadomie nie zaprojektował.

---

## 2. Propozycje: żeby zakładki żyły

Kolejność = stosunek efektu do roboty.

**TEAM — twarze na liście płac.**
Portrety już są cache'owane (`CandidateFaces`). Wystarczy zapamiętać `PortraitSeed` w `Hire` i lista „ON THE PAYROLL" przestaje być tabelką, a staje się zespołem. Jedno pole w save.

**TEAM — kafelek stanowiska podświetla się, gdy ktoś tam pracuje.**
Dziś zmienia tylko tło. Delikatna poświata w kolorze stanowiska + licznik, który „tyka" przy zatrudnieniu, pokazałby kształt firmy jednym spojrzeniem.

**SITE — ludzie w biurze odpowiadają zatrudnionym.**
Grupa `Staff` w prefabach LVL 1/LVL 2 jest pusta. Jeden model na zatrudnionego, siedzący przy biurku, i biuro przestaje być pustą sceną z jednym założycielem. Reużywa `FounderPresence`.

**MAIL — koperta w kolorze kanału.**
Wiersz listu od kandydata mógłby mieć lewą krechę w kolorze Remote/Agency/Specialist. Zero nowych plików, sam USS.

**Agencja Pracy — skan formularza w tle.**
Jeden lekko pożółkły PNG jako tło (`E-11/b`) zrobiłby dla klimatu tej strony więcej niż wszystko inne razem. 800×1000, bardzo niski kontrast.

**get-admin.hck — animowany pasek podczas wyszukiwania.**
Dziś kandydat pojawia się natychmiast po opłaceniu. Dwie sekundy „przeszukiwanie rejestru" sprzedałyby te pieniądze.

**RESEARCH / FLEET — ikony er i generacji.**
Napisy „ERA – Foundations" są już białe i większe, ale nadal to sam tekst. Cztery ikony er ustawiłyby całą zakładkę.

---

## 3. Co już nie jest potrzebne

- Ikony poziomów umiejętności 1–5 do zatrudniania — **stary system kafelków z cyframi został usunięty**, poziom jest teraz liczbą 1–100 i suwakiem.
