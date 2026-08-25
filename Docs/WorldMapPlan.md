# Mapa świata w tworzeniu postaci — jak ją zrobić

Przygotowanie do następnej sesji. Pytałeś, czy da się wziąć coś z internetu i czy da się uzyskać
realistyczną mapę z przybliżaniem i podświetlaniem kontynentów, a po wybraniu — państw.

**Krótka odpowiedź: tak, i nie potrzebujemy do tego obrazka.**

---

## Dlaczego nie zdjęcie satelitarne

Kusi, żeby wziąć teksturę NASA Blue Marble (jest w domenie publicznej) i na niej rysować. Trzy
powody, żeby tego nie robić:

1. **Zdjęcie nie wie, gdzie kończy się Polska.** Podświetlenie kraju wymaga kształtu, a nie pikseli.
   I tak potrzebujemy geometrii, więc zdjęcie to dodatkowa warstwa, nie zamiast.
2. **Waga.** Blue Marble w rozdzielczości, która zniesie przybliżenie do pojedynczego kraju, to
   kilkaset megabajtów. GitHub odrzuca pojedynczy plik powyżej 100 MB — już raz nas to trafiło.
3. **Zasada kierunku artystycznego.** Grafika leży pod interfejsem: równomiernie ciemna, bez
   jasnych punktów. Zdjęcie satelitarne jest dokładnie odwrotnością tego.

---

## Co proponuję: Natural Earth

**[Natural Earth](https://www.naturalearthdata.com)** to zbiór danych kartograficznych w **domenie
publicznej** — bez licencji, bez atrybucji, bez ryzyka w publicznym repo. Robią go kartografowie z
NACIS i jest standardem w tego typu zastosowaniach.

Bierzemy jeden plik: `ne_110m_admin_0_countries` (granice państw, skala 1:110 mln). To około 200 kB
w formacie GeoJSON i zawiera **wszystkie 258 jednostek administracyjnych z kodami ISO**.

```
Assets/_ScalingLaws/Resources/Map/countries.json     ~200 kB, domena publiczna
```

### Dlaczego akurat 1:110 mln

To najmniej szczegółowa wersja, jaką wydają, i to jest zaleta. Przy przybliżeniu do kontynentu
wygląda czysto, a nie potrzebujemy fiordów Norwegii z dokładnością do kilometra. Jeśli po testach
uznamy, że przy maksymalnym zoomie brakuje szczegółu, jest `50m` (~1,5 MB) — wtedy podmieniamy jeden
plik i nic więcej.

---

## Jak to zagra z tym, co już mamy

`UI/WorldMapElement.cs` **już rysuje mapę Painter2D** z ręcznie wpisanych wielokątów w
znormalizowanych współrzędnych, z przezroczystymi przyciskami do trafiania. Cała ta konstrukcja
zostaje — zmienia się tylko **skąd biorą się wielokąty**.

```
teraz:  garść ręcznie wpisanych punktów, 5 kontynentów, 2 nieklikalne
potem:  te same rysowanie i trafianie, punkty czytane z Natural Earth, 258 państw
```

### Trzy rzeczy do zbudowania

**1. Import w edytorze, nie w czasie gry.**
`Editor/MapImporter.cs` czyta GeoJSON raz i zapisuje `ScriptableObject` z gotowymi tablicami
`Vector2`. Parsowanie 200 kB JSON-a przy każdym otwarciu kreatora to sekunda, której nikt nie
zobaczy, ale nie ma powodu jej płacić. Ta sama zasada, co przy `Loc.cs`: generujemy, nie parsujemy.

**2. Rzutowanie.**
Natural Earth podaje stopnie. Potrzebujemy jednej funkcji `(lon, lat) -> Vector2`.
Proponuję **Robinsona** — to rzutowanie, które ludzie rozpoznają jako „mapa świata", bez
grenlandzkiego absurdu Merkatora. Kilkanaście linijek, tabela współczynników jest publiczna.

**3. Kamera.**
Jedno pole `Rect view` w znormalizowanych współrzędnych. Przybliżenie do kontynentu to animacja
tego prostokąta, a nie skalowanie elementu — dzięki temu **grubość linii zostaje stała**, co jest
całą różnicą między mapą a powiększonym obrazkiem.

```
świat        -> klik na kontynent -> Rect kontynentu -> rysują się państwa
kontynent    -> klik na państwo   -> podświetlenie + karta z parametrami
```

### Trafianie w klik

Dziś są przezroczyste przyciski na kontynentach. Przy 258 państwach to 258 przycisków i o jeden
element za dużo. Zamiast tego: **test „punkt w wielokącie"** przy kliknięciu na całą mapę. Klasyczny
ray casting, kilkanaście linijek, sprawdza tylko państwa widoczne w bieżącym `Rect`.

---

## Ile to jest roboty

| Krok | Skala |
|---|---|
| Import GeoJSON do ScriptableObject | pół dnia |
| Rzutowanie Robinsona + testy | 2 godziny |
| Rysowanie z animowanym `Rect` | pół dnia |
| Trafianie w klik i podświetlanie | 2 godziny |
| Podpięcie pod `WorldRegions` (16 państw, które mamy) | 2 godziny |

Realnie: **jedna sesja na działającą wersję**, druga na dopieszczenie.

---

## Czego **nie** proponuję i dlaczego

- **Gotowej paczki z Asset Store.** Repo jest publiczne, a EULA Asset Store zabrania
  redystrybucji. Mielibyśmy to samo, co z meblami do biura: brakujące referencje po sklonowaniu.
- **Mapbox / Google Maps.** Wymagają klucza API i połączenia z siecią. Ta gra nie ma ani jednej
  linijki kodu sięgającej do internetu i to jest świadoma decyzja, ta sama co w PC Workman.
- **SVG z Wikipedii.** Większość map świata na Wikimedia Commons jest na CC BY-SA, co znaczy
  **wirusową licencję na cały projekt**. Natural Earth jest w domenie publicznej i nie ma tego
  problemu.

---

## Jedna decyzja dla Ciebie przed startem

Czy po wybraniu kontynentu ma być **widok całego kontynentu z państwami**, czy raczej
**lista państw obok mapy**, a mapa tylko podświetla to, na co najedziesz?

Pierwsze jest ładniejsze. Drugie jest czytelniejsze przy 16 państwach, które faktycznie mamy w
`WorldRegions` — bo na mapie kontynentu klikalne będą cztery, a reszta będzie wyglądać na zepsutą.

Moja rekomendacja: **drugie**, i dołożyć państwa do `WorldRegions`, jak będą potrzebne. Mapa
podświetlająca 4 z 50 państw wygląda na niedokończoną, a lista obok mapy wygląda na decyzję.
