# Grafiki, których brakuje

Stan na 2026-08-24. Zestawienie wyprodukowane z kodu, nie z pamięci: każda pozycja to nazwa pliku,
o którą interfejs realnie prosi przez `Resources.Load`, albo miejsce, gdzie dziś rysuje się
zastępnik.

**Nic z tego nie wywala gry.** Każdy loader w tym projekcie ma fallback: pusta plakietka, rysowana
płytka albo podpis „grafiki jeszcze nie ma". Dlatego brak pliku nigdy nie zgłasza się sam i dlatego
ta lista musi powstawać ze skryptu.

---

## Zasada dla wszystkiego poniżej

Grafika w tej grze leży **pod interfejsem**. Równomiernie ciemna, niski kontrast wewnętrzny, żadnego
jasnego punktu, nic przy krawędziach, **nigdy tekstu ani logo w obrazie**.

Test: połóż biały wersalik w lewym górnym rogu i kwotę na dole. Jeśli którekolwiek jest trudne do
odczytania, jest za jasna. Obraz, który sam w sobie wygląda świetnie, zwykle jest do tego zły.

Pliki wchodzą do `Assets/_ScalingLaws/Resources/<folder>/<nazwa>.png` (lub `.jpg`). Nazwa musi być
dokładnie taka jak w kolumnie **plik** — kod prosi o nią po nazwie.

---

## 1. Ikony badań — ZROBIONE ✅

**Domknięte 2026-08-24.** Sześć plików leżało w `Art/Research/` i nigdy nie trafiło do
`Resources/Research/`, a `Resources.Load` nie widzi niczego poza folderem Resources, więc były to
ikony, których gra fizycznie nie mogła narysować. Dwie kolejne (`research_hybrid`,
`research_recursive`) leżały w Resources i nikt ich nie nazywał w `ResearchIcons.cs`.

Wszystkie osiem przeskalowane raz do 256×256 pod resztę i wpisane do kodu.

```
nazwane przez kod: 50 z 50      węzły bez ikony: brak
```

## 2. Biura — 2 brakuje, 2 nienazwane

**Folder:** `Resources/Offices/` · **Rozmiar:** 1072×460 (2,33:1), resample raz
**Styl:** wnętrze, ciemne, szeroki kadr; to jest zdjęcie miejsca, nie renderu z gry

| poziom | plik | jest? |
|---|---|---|
| LVL 0 Garaż | `office_house` | ✅ |
| LVL 1 Small office hub | `office_smallhub` | ❌ **nazwany w katalogu, pliku nie ma** |
| LVL 2 Big company hub | `office_bighub` | ❌ **nazwany w katalogu, pliku nie ma** |
| LVL 3 Campus | — | ❌ nie ma nawet nazwy |
| LVL 4 Multi-site | — | ❌ nie ma nawet nazwy |

Dwa pierwsze są pilne: ekran nieruchomości pokazuje dziś dla nich podpis zamiast zdjęcia, a to
najczęściej odwiedzany ekran po siedzibie.

---

## 3. Procesor na ekranie ULEPSZEŃ — 1 brakuje

**Plik:** `Resources/Cards/chip_model.png` · **Rozmiar:** 440×300
**Styl:** krzem z bliska, ciemny, lekko rozmyty; to ma być „zdjęcie tego, co ulepszasz"

Dziś prawy panel rysuje **narysowaną kością zastępczą** z pinami i nazwą modelu na środku. Działa,
ale to jedyne miejsce w grze, gdzie widać, że czegoś brakuje.

---

## 4. Banery stron — 5 ekranów bez baneru

**Folder:** `Resources/Banners/` · **Rozmiar:** 1600×230 po przycięciu (baner ma 112px wysokości)

Dziesięć jest. Te ekrany nie mają żadnego i wyglądają nago obok reszty:

| ekran | proponowany plik |
|---|---|
| MARKETING | `background_marketing` |
| MODEL | `background_model` |
| WIADOMOŚCI | `background_news` |
| @ POCZTA | `background_mail` |
| ZESPÓŁ → zatrudnianie | `background_hiring` |

Po dodaniu wpisać w `GameShell.BannerFor`.

---

## 5. Pakiety hostingu — 1 brakuje

**Folder:** `Resources/Hosting/`

Są dwa (`hosting_renting`, `hosting_datacenter`), a katalog ma **trzy pakiety** (Growth cluster,
Edge tier, Bulk allocation). Ekran MOC rysuje je dziś bez rozróżnienia.

---

## 6. Czego **nie** brakuje

Żeby nie zamawiać dwa razy:

- **Ikony paska dolnego** — 15 nazwanych, 16 plików ✅
- **Logotypy rywali** — 13 nazwanych, 13 plików ✅
- **Kafelki ULEPSZEŃ** — wszystkie 11 z Twoich zdjęć, przerobione ✅
- **Kafelki kredytów** — 5, narysowane ✅
- **Ikony umiejętności** — 7 ✅
- **Wygląd założyciela** — 11 ✅
- **Etapy tworzenia modelu** — 6 (`newmodel_1..6`) ✅
- **Ikony badań** — 50 z 50 ✅

---

## Kolejność, gdybym miał wybierać

1. **`office_smallhub` i `office_bighub`** — nazwane, brakujące, na często odwiedzanym ekranie
2. **`chip_model`** — jedyne widoczne „tu miało coś być"
3. **Pięć banerów** — kosmetyka, ale wyrównuje grę do jednego poziomu
4. **Trzeci hosting** — najmniej pilne

Ikony badań spadły z tej listy — są zrobione.

Jeśli nie chcesz zamawiać ikon badań, mogę je dorysować tak jak dziesięć poprzednich: 300×300,
pierścień, ciemna kreska, pasują do dwudziestu dwóch istniejących.
