using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// One published version of a model, and how many of its users are on it.
    ///
    /// **A version is not replaced by the next one.** That is the whole point of this type. Shipping
    /// an update does not move everybody onto it — it offers them something, and if the something is
    /// worse than what they already had, most of them stay where they are. Every real product works
    /// this way and almost no tycoon game models it.
    /// </summary>
    public sealed class ModelVersion
    {
        public ModelVersion(string name, GameDate releasedOn, double capability,
            double priceUsdPerMonth, double freeTokensPerDay)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled" : name.Trim();
            ReleasedOn = releasedOn;
            Capability = Math.Max(0.0, capability);
            PriceUsdPerMonth = Math.Max(0.0, priceUsdPerMonth);
            FreeTokensPerDay = Math.Max(0.0, freeTokensPerDay);
        }

        public string Name { get; }
        public GameDate ReleasedOn { get; }

        /// <summary>What this version scores. The next one is not guaranteed to beat it.</summary>
        public double Capability { get; }

        public double PriceUsdPerMonth { get; }
        public double FreeTokensPerDay { get; }

        /// <summary>Share of this model's users, zero to one. Moves a little every day.</summary>
        public double Adoption { get; private set; }

        internal void SetAdoption(double share) => Adoption = Math.Clamp(share, 0.0, 1.0);

        public override string ToString() => $"{Name} ({Adoption:P0})";
    }

    /// <summary>
    /// Every version of one model, and where its users actually are.
    ///
    /// **Users drift, they do not teleport.** Each day a slice of the audience moves towards
    /// whichever version appeals to them most, and the rest stay put. Two consequences fall out of
    /// that one rule, and both of them are the reason to build it:
    ///
    /// A bad update does not destroy the product. It sits at a low share while the version people
    /// liked keeps most of the audience, and the player can see exactly that in the list.
    ///
    /// A good update does not land instantly either. Adoption climbs over weeks, so shipping the
    /// day before a rival does still loses you the month.
    ///
    /// Appeal is capability against price, which is the same trade the market model uses at the
    /// company level. A version that is better and cheaper wins quickly; one that is better and
    /// dearer wins slowly; one that is worse and dearer never wins at all.
    /// </summary>
    public sealed class ReleaseLine
    {
        /// <summary>
        /// Share of the gap the audience closes each day.
        ///
        /// Twelve per cent puts a clearly better version at about ninety per cent adoption after a
        /// fortnight, which is roughly how a real update rolls out and slow enough that the player
        /// feels the delay.
        /// </summary>
        public const double DriftPerDay = 0.12;

        /// <summary>
        /// How much of the audience takes a new version on day one regardless.
        ///
        /// Automatic updates and people who always take the newest thing. It is what stops a bad
        /// release reading as no release at all, and it is why a bad update still costs something.
        /// </summary>
        public const double DayOneAdoption = 0.25;

        /// <summary>
        /// How hard price pushes against capability.
        ///
        /// A version twice the price needs to be meaningfully better to hold anybody. Kept low
        /// enough that quality is still the main driver, because this is a game about models.
        /// </summary>
        public const double PriceWeight = 0.55;

        /// <summary>Versions older than this stop being offered and their users are moved on.</summary>
        public const int RetireAfterVersions = 8;

        /// <summary>
        /// How decisively the better version wins.
        ///
        /// **Splitting the audience in proportion to appeal was wrong and a test caught it.** That is
        /// the shape for choosing between vendors, where a product half as good still keeps a third
        /// of the market because it is somebody's supplier and switching is a procurement decision.
        /// Choosing between two versions of the same product is not that: it is one button, and the
        /// only thing keeping anybody on the old one is that they have not pressed it. A strictly
        /// better version at the same price settled at 64% forever, which no real update does.
        ///
        /// Raising appeal to the fourth power makes staying put an act of inertia rather than a
        /// preference. A version that is clearly better takes about ninety per cent; one that is
        /// merely a bit better still leaves a real minority behind; one that is worse is left with
        /// the people who updated automatically and nobody else.
        /// </summary>
        public const double Decisiveness = 4.0;

        private readonly List<ModelVersion> versions = new();

        /// <summary>Newest last, which is the order they were shipped in.</summary>
        public IReadOnlyList<ModelVersion> Versions => versions;

        public ModelVersion Newest => versions.Count > 0 ? versions[^1] : null;

        public int Count => versions.Count;

        /// <summary>
        /// The name to show as what this update follows.
        ///
        /// "Base" when nothing has shipped yet, which is what the release screen prints in italics
        /// under the model name.
        /// </summary>
        public string PreviousName => Newest?.Name ?? "Base";

        /// <summary>
        /// Publishes a version and gives it its day-one share, taken from everybody else.
        ///
        /// The share is taken proportionally rather than off the top of the leader, because an
        /// automatic update pulls from wherever people happen to be.
        /// </summary>
        public ModelVersion Publish(string name, GameDate on, double capability,
            double priceUsdPerMonth, double freeTokensPerDay)
        {
            var version = new ModelVersion(name, on, capability, priceUsdPerMonth, freeTokensPerDay);

            if (versions.Count == 0)
            {
                version.SetAdoption(1.0);
                versions.Add(version);
                return version;
            }

            foreach (var older in versions)
            {
                older.SetAdoption(older.Adoption * (1.0 - DayOneAdoption));
            }

            version.SetAdoption(DayOneAdoption);
            versions.Add(version);

            Retire();
            Normalise();

            return version;
        }

        /// <summary>
        /// Moves a slice of the audience towards whatever they would rather be using.
        ///
        /// Called once a day. Everything above is arithmetic on one number per version, so a company
        /// with six models and eight versions each costs nothing worth measuring.
        /// </summary>
        public void Advance()
        {
            if (versions.Count < 2)
            {
                return;
            }

            var appeals = versions.Select(Appeal).ToArray();
            var best = appeals.Max();

            if (best <= 0.0)
            {
                return;
            }

            // Measured against the best version before being raised to the power, so the numbers
            // stay near one whatever scale capability has reached by year twelve. Dividing every
            // appeal by the same figure cannot change the ratios, which are all this uses.
            var weights = appeals.Select(appeal => Math.Pow(appeal / best, Decisiveness)).ToArray();
            var total = weights.Sum();

            if (total <= 0.0)
            {
                return;
            }

            // Where the audience would settle if nobody had any inertia at all.
            for (var index = 0; index < versions.Count; index++)
            {
                var target = weights[index] / total;
                var now = versions[index].Adoption;

                versions[index].SetAdoption(now + (target - now) * DriftPerDay);
            }

            Normalise();
        }

        /// <summary>
        /// How much a version is worth to somebody choosing between them.
        ///
        /// Capability over price, softened so that a free-to-cheap difference does not swamp a real
        /// quality difference. Squared on capability because the gap between a good model and a
        /// slightly better one matters more to users than the same gap lower down.
        /// </summary>
        private static double Appeal(ModelVersion version)
        {
            var quality = Math.Max(0.1, version.Capability);
            var price = 1.0 + version.PriceUsdPerMonth * PriceWeight * 0.01;

            return quality * quality / price;
        }

        /// <summary>
        /// Drops versions nobody should still be offered and hands their users to the newest.
        ///
        /// Without this a ten-year campaign carries forty versions of one model, and the list the
        /// player reads becomes an archive rather than a decision.
        /// </summary>
        private void Retire()
        {
            while (versions.Count > RetireAfterVersions)
            {
                var dropped = versions[0];
                versions.RemoveAt(0);

                var newest = versions[^1];
                newest.SetAdoption(newest.Adoption + dropped.Adoption);
            }
        }

        /// <summary>
        /// Forces the shares back to exactly one.
        ///
        /// Drift and retirement both leave rounding behind, and a list that adds up to 99% is a list
        /// the player will notice and not trust.
        /// </summary>
        private void Normalise()
        {
            var total = versions.Sum(version => version.Adoption);

            if (total <= 0.0)
            {
                if (versions.Count > 0)
                {
                    versions[^1].SetAdoption(1.0);
                }

                return;
            }

            foreach (var version in versions)
            {
                version.SetAdoption(version.Adoption / total);
            }
        }

        /// <summary>
        /// The capability the market actually sees, weighted by who is on what.
        ///
        /// **This is what makes a bad update cost something real.** The company's score is not its
        /// newest version, it is the average of what its users are running — so shipping something
        /// worse drags the number down by exactly the share of people who took it.
        /// </summary>
        public double EffectiveCapability() =>
            versions.Count == 0
                ? 0.0
                : versions.Sum(version => version.Capability * version.Adoption);

        /// <summary>The price the average user is paying, for the revenue side.</summary>
        public double EffectivePriceUsdPerMonth() =>
            versions.Count == 0
                ? 0.0
                : versions.Sum(version => version.PriceUsdPerMonth * version.Adoption);

        /// <summary>Rebuilds a line from a save, shares as written.</summary>
        public static ReleaseLine Restore(
            IEnumerable<(string Name, int Day, double Capability, double Price, double Free,
                double Adoption)> saved)
        {
            var line = new ReleaseLine();

            if (saved == null)
            {
                return line;
            }

            foreach (var entry in saved)
            {
                var version = new ModelVersion(entry.Name, new GameDate(entry.Day),
                    entry.Capability, entry.Price, entry.Free);

                version.SetAdoption(entry.Adoption);
                line.versions.Add(version);
            }

            if (line.versions.Count > 0)
            {
                line.Normalise();
            }

            return line;
        }
    }
}
