using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// What a map site is for, at the broadest grain. Decides which existing system, if any, a pin
    /// is a doorway into.
    /// </summary>
    public enum MapSiteKind
    {
        /// <summary>A place on <see cref="OfficeCatalog"/>'s ladder, given a body on the map.</summary>
        OfficeLease = 0,

        /// <summary>A house the founder could move into. Net worth, not headcount.</summary>
        PropertyListing = 1,

        /// <summary>A physical stop on the compute ladder, beyond the basement every company starts with.</summary>
        ServerFacility = 2,

        /// <summary>Somewhere an event actually happens. Points at an existing <see cref="ParkSite"/> or <see cref="MallSite"/>.</summary>
        EventVenue = 3,

        /// <summary>One of <see cref="PowerPlantCatalog"/>'s two sites, with a stake a rival could also buy into.</summary>
        PowerPlantStake = 4,

        /// <summary>A door onto one of the three hiring channels the game already has.</summary>
        JobAgency = 5,

        /// <summary>Where the January bill comes from. One of these; the Civic district's own blurb already names it.</summary>
        TaxOffice = 6,

        /// <summary>A car on a forecourt. Nothing in the simulation reads this yet — see the note on the catalog itself.</summary>
        CarDealership = 7
    }

    /// <summary>
    /// One place on the map worth clicking, in the status vocabulary <c>Docs/SIMULATION_SYSTEM_AUDIT.md</c>
    /// already uses: a site is either backed by a real system today, or it is surveyed ground waiting
    /// for one.
    ///
    /// Position and radius follow <see cref="CityLayout"/>'s convention: metres from the terrain's
    /// south-west corner, the same space every district, road and building block in the city already
    /// uses.
    /// </summary>
    public sealed class MapSiteDefinition
    {
        public MapSiteDefinition(string id, string displayName, MapSiteKind kind, MapCategory category,
            string districtId, MapPoint position, float radius, int tier, string decisionBlurb,
            bool systemExists)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            Category = category;
            DistrictId = districtId;
            Position = position;
            Radius = radius;
            Tier = tier;
            DecisionBlurb = decisionBlurb;
            SystemExists = systemExists;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public MapSiteKind Kind { get; }
        public MapCategory Category { get; }
        public string DistrictId { get; }
        public MapPoint Position { get; }
        public float Radius { get; }

        /// <summary>Where it sits on its own ladder, if it has one. Zero where there is only one of it.</summary>
        public int Tier { get; }

        /// <summary>
        /// The decision, possibility or risk this site is supposed to make legible. Per the rule in
        /// <c>Docs/CITY_MAP_PLAN.md</c>: a site with nothing to decide is decoration, and decoration
        /// does not belong on this map.
        /// </summary>
        public string DecisionBlurb { get; }

        /// <summary>
        /// True when a real Data catalog already computes this site's economics — <see cref="OfficeCatalog"/>,
        /// <see cref="PowerPlantCatalog"/>, or one of the three hiring channels. False means the pin
        /// is surveyed and the rule behind it is not written yet: the same MISSING/EXTEND distinction
        /// <c>Docs/SIMULATION_SYSTEM_AUDIT.md</c> already uses, so this file does not invent a second
        /// vocabulary for the same idea.
        /// </summary>
        public bool SystemExists { get; }
    }

    /// <summary>
    /// Stage 4 of <c>Docs/CITY_MAP_PLAN.md</c>: the points of interest the plan asks for, laid out
    /// inside the eight districts <see cref="CityLayout"/> already surveyed.
    ///
    /// **DRAFT. Not reviewed by the author and not run through Unity.** Every position sits inside an
    /// existing district's centre and radius from <see cref="CityLayout.Districts"/>, so nothing here
    /// should land on a hillside or in the bay by construction — but stage 1 already taught this
    /// project that a coordinate which compiles can still be a building on a cliff, which is exactly
    /// what <c>CitySnapshot</c> and <c>CityFlight</c> exist to catch. Run one before trusting a single
    /// position in this file. <see cref="MapSiteCatalogTests"/> only checks that positions sit inside
    /// their district's own bounds; it cannot see whether two sites sit on top of each other, because
    /// that needs a render, not an assertion — the same limit <c>TabProofTests</c> was written for.
    ///
    /// **Five kinds point at a system that already runs today.** The five <see cref="OfficeCatalog"/>
    /// tiers beyond the garage, the two <see cref="PowerPlantCatalog"/> sites, and the three hiring
    /// channels (headhunter, state register, contract board) that <c>GETTING_STARTED.md</c> already
    /// describes. Tax has exactly one site because <see cref="CityLayout.Districts"/> already wrote
    /// the joke: the Civic Center's own blurb names "the tax office" without this file's help.
    ///
    /// **Three kinds are surveyed and empty.** <see cref="MapSiteKind.PropertyListing"/>, the growth
    /// stops in <see cref="MapSiteKind.ServerFacility"/> beyond the basement and the five halls to rent
    /// in River Works have no Data catalog behind them yet — see <c>Docs/CAMPAIGN_STABILITY_AND_FEATURE_AUDIT.md</c>, "build the
    /// compute and hardware page before another economic system," which this is a map layer on top
    /// of, not a replacement for. <see cref="MapSiteKind.CarDealership"/> is also the site of a
    /// promise the tutorial already makes and the game does not keep: step 49 has Emil offer "cars to
    /// pick from," which <c>anything/Otwarte pytania i znaleziska/samouczek-przeczytany-jak-gracz.md</c>
    /// flagged as untrue. Building the dealership is most of what it would take to close that gap
    /// honestly, rather than by deleting Emil's line.
    ///
    /// **Power plant positions are the most speculative pair in this file, and they sit outside every
    /// district's own circle on purpose.** <see cref="PowerPlantCatalog"/> carries a name and an
    /// economy for each site and no coordinate — "by the river" and "on the coast" are prose, not a
    /// `MapPoint`. The two placed here sit near <see cref="CityLayout.Water"/>'s river and the open
    /// coast west of the port, which is a reading of the name rather than a measurement of anything.
    /// Real power infrastructure does not stand inside a residential or financial district either, so
    /// both are tagged <c>"port"</c> for filtering only; <see cref="MapSiteCatalogTests"/> does not
    /// require them inside Port's drawn radius. <c>Docs/CITY_MAP_PLAN.md</c>'s own first draft had an
    /// "Energy Belt" as a ninth district, on the western hills, and it did not survive into
    /// <see cref="CityLayout.Districts"/> when that file was redrawn against the author's reference.
    /// Whether Energy gets a district of its own is the author's call, not something this file
    /// decides by placing two pins and hoping — move them freely; nothing else references these two
    /// positions.
    /// </summary>
    public static class MapSiteCatalog
    {
        public const string CatalogVersion = "sites-1-draft";

        private static readonly MapSiteDefinition[] Entries =
        {
            // ---- Offices: OfficeCatalog's ladder, given a body --------------------------------------
            // Garage is the founder's own house and already stands as CityLayout.FounderHome; it gets
            // no separate pin here.

            new("office.loft", "Loft", MapSiteKind.OfficeLease, MapCategory.Business,
                "riverdale", new MapPoint(1560f, 360f), 40f, (int)OfficeTier.Loft,
                "Ten desks, real rent, the first place that is not the house. Signing it is the first "
                + "fixed cost the company cannot walk away from.", systemExists: true),

            new("office.floor", "Floor", MapSiteKind.OfficeLease, MapCategory.Business,
                "media", new MapPoint(340f, 900f), 42f, (int)OfficeTier.Floor,
                "Twenty desks on the edge of the media strip. Enough room to hire past the point "
                + "where everybody still fits around one table.", systemExists: true),

            new("office.campus", "Campus", MapSiteKind.OfficeLease, MapCategory.Research,
                "innovation", new MapPoint(1480f, 1360f), 55f, (int)OfficeTier.Campus,
                "Fifty desks next to the university the company will be hiring out of. Announced in "
                + "the catalog with no picture yet, so this pin is ahead of the in-game chooser.",
                systemExists: true),

            new("office.tower", "Tower", MapSiteKind.OfficeLease, MapCategory.Finance,
                "downtown", new MapPoint(1040f, 1080f), 50f, (int)OfficeTier.Tower,
                "A floor in the skyline everybody else is also renting space in. A hundred and "
                + "twenty-five desks and a rent that only makes sense once the company is out of "
                + "reasons to hesitate.", systemExists: true),

            new("office.multisite", "Multi-Site HQ", MapSiteKind.OfficeLease, MapCategory.Finance,
                "downtown", new MapPoint(960f, 960f), 46f, (int)OfficeTier.MultiSite,
                "Not one building. This pin is the headquarters the other sites report to: two "
                + "hundred desks' worth of company, spread further than one map screen shows.",
                systemExists: true),

            // ---- Property: net worth a player can walk past, not headcount --------------------------
            // No Data catalog reads any of these yet. Tiers are the author's own words from the
            // request: houses "różne aż po luksusowe" — ordinary through luxury.

            new("home.starter", "Starter House", MapSiteKind.PropertyListing, MapCategory.Business,
                "riverdale", new MapPoint(1520f, 340f), 22f, 1,
                "A house like the one the company started in, on a street exactly like it. Buying a "
                + "second one is the first time cash becomes something other than runway.",
                systemExists: false),

            new("home.family", "Family House", MapSiteKind.PropertyListing, MapCategory.Business,
                "riverdale", new MapPoint(1720f, 560f), 26f, 2,
                "A bigger plot in the same suburb. Nothing the company does gets faster because of "
                + "it; it is what the founder has to show for the years that did work.",
                systemExists: false),

            new("home.villa", "Villa", MapSiteKind.PropertyListing, MapCategory.Business,
                "greendale", new MapPoint(260f, 1560f), 30f, 3,
                "Up on the shoulder of the hills, long driveway, a view of the bay nobody in "
                + "Riverdale gets. The grand plots CityBlocks already surveyed for Greendale are "
                + "built for exactly this.", systemExists: false),

            new("home.estate", "Estate", MapSiteKind.PropertyListing, MapCategory.Business,
                "greendale", new MapPoint(420f, 1700f), 36f, 4,
                "The most expensive address on the map, and a decision about what the money was "
                + "for: reinvested it grows the company, spent here it never comes back as anything "
                + "but a number on a net worth screen.", systemExists: false),

            // ---- Server facilities: growth beyond the basement, all in Waterfront & Port ------------
            // "Hardware comes through here and so does everybody attending whatever is on at the expo
            // halls. Land is cheap and the power is already run in" — the Port district's own blurb
            // in CityLayout.cs, written before this file existed and already arguing for putting
            // these here. Spread along the port so a bigger tier reads as more land, not a taller
            // number on the same footprint.

            // **Re-positioned 2026-09-15, twice.** The first spread (out to x=750, z=460) put three
            // sites on the river. The next attempt, guessed from the same by-hand reading of
            // CityLayout.Water's control points, put five sites in the river instead — worse, not
            // better, because that reading of the water's shape was wrong both times. What actually
            // settled it: a small Editor script that calls CityTerrainBuilder.HeightAt directly
            // across a grid over the district and writes out which points come back above sea
            // level. Every position below is inside a patch that sampled dry on every point checked
            // within about 30m, south of the river's crossing rather than west of it.

            new("server.outpost", "First Rack Hall", MapSiteKind.ServerFacility, MapCategory.Compute,
                "port", new MapPoint(520f, 190f), 22f, 1,
                "Ugly on purpose: a shed with a loading bay, the first place the company's own "
                + "silicon lives instead of somebody else's cluster. Where ComputeTier."
                + "ColocatedServers should stand once the room has a scene.", systemExists: false),

            new("server.hall", "Server Hall", MapSiteKind.ServerFacility, MapCategory.Compute,
                "port", new MapPoint(570f, 180f), 26f, 2,
                "A proper warehouse floor, still air-cooled. What a colocated tier looks like once "
                + "the company has filled the first rack and ordered more.", systemExists: false),

            new("server.cooledhall", "Cooled Hall", MapSiteKind.ServerFacility, MapCategory.Compute,
                "port", new MapPoint(620f, 200f), 30f, 3,
                "Real cooling towers on the roof — the visual answer to the seven-cards-beat-eight "
                + "finding from the README: a company that keeps this hall below its throttling "
                + "point should look different from one that does not.", systemExists: false),

            new("server.campus", "Compute Campus", MapSiteKind.ServerFacility, MapCategory.Compute,
                "port", new MapPoint(600f, 250f), 34f, 4,
                "Several halls on one plot: what ComputeTier.OwnDatacenter should look like from the "
                + "map. The $80M datacenter already exists in the economy and has never had a "
                + "building.", systemExists: false),

            new("server.megasite", "Megasite", MapSiteKind.ServerFacility, MapCategory.Compute,
                "port", new MapPoint(520f, 220f), 30f, 5,
                "The far edge of the cluster, and the only building on this map sized to matter on "
                + "its own rather than as one of a row. Smaller than first drawn — the wide radius "
                + "the original number wanted was itself most of what put it in the river. Nothing "
                + "in the economy reaches this tier yet; it exists so the ladder visibly has a top.",
                systemExists: false),

            // ---- Server halls to rent: River Works -------------------------------------------------
            // Gosia's design, 2026-09-16: a small works district on the river bank with halls to
            // rent rather than a ladder to climb — small, medium and large back from the water, and
            // two on the bank itself that pipe the river through their cooling and charge more for
            // it. The cooling is the point of the district, so where each hall stands relative to the
            // water is part of its entry, not decoration: the two on the water stand on the strip
            // between the river street and the bank, measured at 48 to 56 metres deep there.

            new("server.riverworks.small", "Rack Room", MapSiteKind.ServerFacility, MapCategory.Compute,
                "riverworks", new MapPoint(703f, 652f), 20f, 1,
                "The smallest hall on the river works: one row of racks behind a roller door, rented "
                + "by the month. Air-cooled, and a street back from the water, which is what keeps the "
                + "rent low.", systemExists: false),

            new("server.riverworks.medium", "Workshop Hall", MapSiteKind.ServerFacility, MapCategory.Compute,
                "riverworks", new MapPoint(650f, 623f), 24f, 2,
                "Room for a proper cluster without a campus's rent. Air-cooled like everything back "
                + "from the bank, so it runs warmer in summer and the price says so.", systemExists: false),

            new("server.riverworks.riverside", "Riverside Hall", MapSiteKind.ServerFacility, MapCategory.Compute,
                "riverworks", new MapPoint(758f, 540f), 28f, 3,
                "On the bank itself, with intake pipes straight into the river. The water does the "
                + "work chillers do everywhere else, so the rent is higher and the power bill lower — "
                + "the trade this district exists for.", systemExists: false),

            new("server.riverworks.rivercampus", "River-Cooled Campus", MapSiteKind.ServerFacility, MapCategory.Compute,
                "riverworks", new MapPoint(700f, 512f), 34f, 4,
                "The biggest hall on the water: its own pump house, intakes upstream and an outfall "
                + "below. The dearest lease in River Works, and the one that stays coolest under full "
                + "load.", systemExists: false),

            new("server.riverworks.inland", "Inland Campus", MapSiteKind.ServerFacility, MapCategory.Compute,
                "riverworks", new MapPoint(811f, 667f), 34f, 4,
                "As large as the river campus, but behind the streets instead of on the bank: every "
                + "watt of heat leaves through chillers on the roof. Cheaper to rent, dearer to run.",
                systemExists: false),

            // ---- Event venues: pointing at ground CityBlocks already surveyed -----------------------
            // Coordinates copied from CityBlocks.cs rather than referenced, so this file has no
            // static-initialisation-order dependency on it. If those positions move, these three
            // pins have to move with them by hand.

            new("event.park", "Bayview Park", MapSiteKind.EventVenue, MapCategory.Events,
                "park", new MapPoint(990f, 1450f), 0f, 0,
                "AI Frontier Expo and Global Model Awards, per the calendar in "
                + "Docs/CITY_MAP_PLAN.md. Already a ParkSite with a lake and an event ground; this "
                + "pin only adds the calendar link, not new geometry.", systemExists: false),

            new("event.gallery", "Bayview Gallery", MapSiteKind.EventVenue, MapCategory.Events,
                "park", new MapPoint(790f, 1300f), 0f, 0,
                "Compute & Infrastructure Expo and Creator Intelligence Festival. Already a MallSite "
                + "with the car park the plan's notes insisted on; this pin only adds the calendar "
                + "link.", systemExists: false),

            new("event.civicsquare", "Civic Square", MapSiteKind.EventVenue, MapCategory.Events,
                "civic", new MapPoint(930f, 690f), 0f, 0,
                "Responsible AI Forum and the press conferences a scandal calls for. Already a "
                + "ParkSite (civic_gardens) sized for a crowd rather than a festival — the request "
                + "for a \"rynek miasta\" is this site.", systemExists: false),

            new("event.mediaplaza", "Media Plaza", MapSiteKind.EventVenue, MapCategory.Events,
                "media", new MapPoint(230f, 780f), 60f, 0,
                "Model Research Summit and Capital & AI Forum need a second indoor venue so the "
                + "gallery is not hosting three unrelated event types a year. Proposed, not yet a "
                + "MallSite in CityBlocks.cs — needs the same survey the gallery already got before "
                + "a builder can draw it.", systemExists: false),

            // ---- Power plant stakes: PowerPlantCatalog's two sites, positions are new ---------------

            new("plant.riverside", "Riverside Plant", MapSiteKind.PowerPlantStake, MapCategory.Energy,
                "port", new MapPoint(210f, 260f), 60f, (int)PowerPlantSite.Riverside,
                "Gas turbines, two and a half years to build, cheap and quick. PowerPlantCatalog "
                + "already prices it; a stake a rival could buy into is not built yet.",
                systemExists: false),

            new("plant.coastal", "Coastal Plant", MapSiteKind.PowerPlantStake, MapCategory.Energy,
                "port", new MapPoint(80f, 100f), 70f, (int)PowerPlantSite.Coastal,
                "A nuclear block, nine years and nearly five times the capital, then a tenth of the "
                + "running cost. The bigger of the two footprints on this map after the megasite.",
                systemExists: false),

            // ---- Job agencies: the three hiring channels GETTING_STARTED.md already describes -------

            new("jobs.headhunter", "Headhunter Tower", MapSiteKind.JobAgency, MapCategory.Business,
                "downtown", new MapPoint(1120f, 1000f), 30f, 1,
                "Charges whether or not the candidate signs, and finds people the other two "
                + "channels never see. In the skyline, deliberately: this is the expensive one.",
                systemExists: true),

            // Moved 2026-09-15: the same river that crosses Port turns out to clip the middle of
            // Civic too, and the district's own centre (1075,660) reads 37.4m against a declared
            // flat 52m — a whole-map dry-land sweep found it, the same HeightAt sampling that fixed
            // Port. This site and civic.taxoffice below both sit in the confirmed dry strip north
            // of the crossing now, not at the district's geometric centre.
            new("jobs.stateregister", "State Employment Register", MapSiteKind.JobAgency, MapCategory.Regulation,
                "civic", new MapPoint(1080f, 478f), 16f, 2,
                "Hasn't been redesigned since 2009, per the game's own description of it. Cheapest "
                + "channel, slowest, and it sits exactly where a bureaucratic office belongs.",
                systemExists: true),

            new("jobs.contractboard", "Contract Board", MapSiteKind.JobAgency, MapCategory.Media,
                "media", new MapPoint(250f, 860f), 26f, 3,
                "The informal channel: postings rather than a desk. Placed in the media strip because "
                + "it is the closest thing this city has to a noticeboard district.",
                systemExists: true),

            // ---- Tax office: the Civic Center's own blurb already names it --------------------------

            new("civic.taxoffice", "Tax Office", MapSiteKind.TaxOffice, MapCategory.Regulation,
                "civic", new MapPoint(1170f, 510f), 34f, 0,
                "\"City hall, the AI authority, the tax office and the compliance desk\" — "
                + "CityLayout.cs described this district before this file existed. The January bill "
                + "and every postponement decision belong at this address.", systemExists: true),

            // ---- Car dealerships: cosmetic today, and the fix for a promise Emil already makes ------

            new("cars.port", "Port Motors", MapSiteKind.CarDealership, MapCategory.Business,
                "port", new MapPoint(660f, 230f), 22f, 1,
                "Nothing in the simulation reads this yet. Emil promises \"cars to pick from\" at "
                + "tutorial step 49 and the game has never had one; this is the site for closing "
                + "that gap rather than quietly cutting his line.", systemExists: false),

            new("cars.riverdale", "Riverdale Motors", MapSiteKind.CarDealership, MapCategory.Business,
                "riverdale", new MapPoint(1600f, 330f), 22f, 2,
                "The suburban dealership, on the same road as the starter house. Cosmetic pin, same "
                + "gap as Port Motors.", systemExists: false)
        };

        public static IReadOnlyList<MapSiteDefinition> All => Entries;

        public static IEnumerable<MapSiteDefinition> OfKind(MapSiteKind kind)
        {
            foreach (var entry in Entries)
            {
                if (entry.Kind == kind)
                {
                    yield return entry;
                }
            }
        }
    }
}
