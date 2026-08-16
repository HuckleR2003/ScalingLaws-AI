# CITY_MAP_PLAN — Bayview

Plan budowy mapy miasta. Źródło: notatki autora (`XPRO.txt`) + dwa rendery referencyjne.

**Zasada nadrzędna, dosłownie z notatek:**

> Gracz powinien patrzeć na mapę i myśleć: „na co ja mam teraz wydać pieniądze?", a nie „o, ładne budynki".

Każdy punkt musi dawać **decyzję, możliwość albo ryzyko**. `Park = +2 reputacji` jest złe. `Civic Square = public demos / press events / protests / crisis response` jest dobre.

---

## 1. Rozmiar terenu — decyzja i uzasadnienie

To najważniejsza liczba w całym dokumencie, bo wszystko inne się do niej dowiązuje i późniejsza zmiana jest kosztowna.

**Rekomendacja: `2048 × 2048 m`, heightmap `1025`, w jednym `Terrain`.**

| Rozmiar | Za | Przeciw | Werdykt |
|---|---|---|---|
| 1024 m | lekki, szybki | 8 dzielnic + zatoka + lotnisko = wszystko na sobie | za mały |
| **2048 m** | **dzielnica ~400–600 m, lotnisko się mieści, zatoka ma sens** | — | **to bierzemy** |
| 4096 m | miejsce na przyszłe regiony | 3/4 mapy to puste wzgórza, koszt zapełnienia ogromny | później, jako sąsiedni teren |

**Dlaczego 2048:** dzielnica czytelna w kamerze izometrycznej ma ~400–600 m w poprzek. Osiem dzielnic + woda + pas energetyczny na obrzeżach to ~1800 m. 2048 daje margines bez pustki.

**Dlaczego heightmap 1025 (nie 513, nie 2049):** przy 2048 m to **2 m na próbkę** — dość na wzgórza, klify i brzeg zatoki, za mało na krawężnik (i dobrze, krawężniki robi geometria, nie teren). 2049 to 4× pamięci za rozdzielczość, której kamera z góry nigdy nie zobaczy.

**Pozostałe ustawienia:**

| Parametr | Wartość | Dlaczego |
|---|---|---|
| Terrain height | 400 m | wzgórza jak na renderze; klify nad zatoką bez rozciągania tekstur |
| Poziom wody | y = 40 m | zostawia 40 m zapasu na dno zatoki i port |
| Detail resolution | 512 | trawa tylko w parkach, nie na całej mapie |
| Basemap distance | 2000 | kamera izo widzi całość, więc basemap musi sięgać dalej niż domyślne 1000 |
| Pixel error | 3 | niżej = niepotrzebne trójkąty pod kamerą, która i tak patrzy z góry |

**Origin:** `(0, 0, 0)` w południowo-zachodnim rogu, tak jak wszystkie buildery pomieszczeń w tym projekcie (`OfficeRoomBuilder`, `HubRoomBuilder`). Współrzędne dzielnic w metrach od tego rogu.

---

## 2. Układ dzielnic

Siatka orientacyjna na terenie 2048 × 2048. `x` na wschód, `z` na północ.

| # | Dzielnica | Środek (x, z) | Promień | Kategoria wiodąca |
|---|---|---|---|---|
| 1 | **Residential / Greendale** — start gracza | 380, 1500 | 320 | BUSINESS (dom) |
| 2 | Downtown Financial | 1050, 1000 | 300 | FINANCE |
| 3 | Innovation District | 1450, 1280 | 280 | RESEARCH |
| 4 | Compute / Industrial | 1550, 1650 | 300 | COMPUTE |
| 5 | Media District | 620, 780 | 240 | MEDIA |
| 6 | Waterfront / Port | 900, 420 | 280 | COMPUTE + EVENTS |
| 7 | Energy Belt | 300, 260 | 340 | ENERGY |
| 8 | Civic / Government | 1180, 760 | 220 | REGULATION |

**Zatoka** wcina się z północnego zachodu do centrum (ok. `x 500–1100, z 1100–1900`), rozdzielając Greendale od Downtown — stąd mosty. **Rzeka** schodzi z gór na wschodzie do zatoki, i to na niej stoi hydro.

**Gracz mieszka w Greendale** — mały amerykański domek, długi podjazd, garaż. To stamtąd wyjeżdża autem (mamy już `FounderPresence` i waypointy `Garage`/`Car`).

---

## 3. Etapy budowy

Kolejność jest celowa: **teren przed budynkami, budynki przed systemami, systemy przed eventami**. Każdy etap ma dawać coś grywalnego.

### Etap 1 — teren i woda *(zaczynamy tutaj)*
- `CityTerrainBuilder`: heightmapa proceduralna z ziarna, wzgórza na wschodzie i północy, zatoka, rzeka.
- Płaskie „poduszki" pod każdą z 8 dzielnic — budynki nie mogą stać na skosie.
- Płaszczyzna wody, `Docs`-owy snapshot z góry do oceny.
- **Rezultat:** można obejrzeć ląd i powiedzieć „tak, to jest Bayview".

### Etap 2 — dzielnice jako strefy
- `DistrictCatalog` (Data, czyste): id, nazwa, środek, promień, kategoria, opis.
- Markery i granice stref na terenie; jeszcze bez budynków.
- **Rezultat:** mapa ma nazwy i da się je kliknąć.

### Etap 3 — drogi, mosty, brzeg
- Główne arterie łączące dzielnice, dwa mosty przez zatokę, nabrzeże portu.
- **Rezultat:** miasto wygląda na połączone, nie na 8 wysp.

### Etap 4 — punkty zainteresowania (POI)
- `MapSiteCatalog`: ~35 punktów z listy autora, każdy z **decyzją**, nie z bonusem.
- Wpięcie istniejących systemów: biura (mamy), serwerownie (mamy), regulator (mamy).
- **Rezultat:** mapa zaczyna zastępować część menu.

### Etap 5 — warstwy i filtry
- 8 filtrów, przyciemnienie mapy, glow kategorii, linie infrastruktury (`POWER GRID → DATACENTER → YOUR COMPANY`).
- **Rezultat:** ta rzecz, którą autor nazwał „najbardziej premium elementem UI".

### Etap 6 — pętla NEWS → MAP
- Wiadomość podświetla konkretne miejsce; kamera tam jedzie.
- **Rezultat:** must-have z notatek. Wiadomości przestają być abstrakcyjne.

### Etap 7 — eventy
- 7 wydarzeń z notatek, z częstotliwościami i pełnym kosztorysem stoiska.
- **Rezultat:** kalendarz, na który gracz czeka.

### Etap 8 — energia i konsorcja
- Solar / hydro jako **projekty inwestycyjne** z udziałami; rywale mogą wejść i przejąć większość.
- **Rezultat:** długoterminowe aktywa i najlepszy „😈" z notatek.

---

## 4. Eventy — kalendarz z notatek

| Event | Co ile | Kategoria | Co daje |
|---|---|---|---|
| AI Frontier Expo | 12 mies. | EVENTS | reputacja, zainteresowanie, media |
| Model Research Summit | 18 mies. | RESEARCH | dużo research, mało popularności |
| Compute & Infrastructure Expo | 9 mies. | COMPUTE | kontakty sprzętowe, ceny compute |
| Capital & AI Forum | 12 mies. | FINANCE | pitch; zadłużenie = `Investor confidence: Low` |
| Creator Intelligence Festival | 6 mies. | MEDIA | marketing i zainteresowanie |
| Responsible AI Forum | 12 mies. | REGULATION | niski safety = publiczna krytyka |
| Global Model Awards | 24 mies. | EVENTS | ogromny boost; **nominacja z realnego stanu modelu, nie losowa** |

Każdy event ma mieć: koszt wstępu, koszt stoiska, grupę odbiorców, szansę na media, szansę na kontakt biznesowy, czas trwania, opcję wystąpienia.

---

## 5. Czego to dotknie w istniejącym kodzie

- `OfficeCatalog` → dostaje lokalizację na mapie (dzielnica), bo biura już są.
- `ComputePool` → serwerownie dostają lokalizację i **charakterystykę miejsca** (cena/dostępność/chłodzenie/rozbudowa) — to z notatek, i to jest właściwy moment, żeby compute przestał być jedną liczbą.
- `RegulatoryAction` → dostaje adres: konkretny regulator w Civic District.
- `NewsScreen` → link do miejsca na mapie.
- `MarketingCatalog` → kanały dostają siedziby w Media District.

**Czego nie ruszamy:** `Game.unity` (licznik `PrefabInstance` = 107) i sceny biur. Miasto to osobna scena.
