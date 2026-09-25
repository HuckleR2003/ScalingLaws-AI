using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests
{
    /// <summary>
    /// The card asks real people for real money, so what it says has to be true on the day it says
    /// it. These tests are the difference between a notice and a sales pitch.
    /// </summary>
    public sealed class SupporterCardTests
    {
        private static readonly string[] Keys =
        {
            "support.kicker", "support.title", "support.why", "support.tier_name",
            "support.tier_early", "support.until", "support.open", "support.free"
        };

        [Test]
        public void EverySentenceExistsInBothLanguages()
        {
            var was = Loc.Current;
            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;
                    foreach (var key in Keys)
                    {
                        var text = Loc.T(key);
                        Assert.That(text, Is.Not.Null.And.Not.Empty, key);
                        Assert.That(text, Is.Not.EqualTo(key),
                            $"{key} is rendering as its own name in {language}");
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }

        /// <summary>
        /// **The honesty ratchet, and it is the reason this fixture exists.**
        ///
        /// The hundred zloty tier advertised outside the game also promises a candidate the player
        /// can hire on the TEAM screen. Nothing in `Simulation/` knows anything about supporters, so
        /// that half is not built, and a card inside the game claiming it would be selling something
        /// that does not exist.
        ///
        /// When it is built, this test fails, and the right repair is to delete it rather than to
        /// loosen it: at that point the promise is true and belongs on the card.
        /// </summary>
        [Test]
        public void TheCardDoesNotPromiseTheHiringTierUntilItIsBuilt()
        {
            var was = Loc.Current;
            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;
                    var copy = string.Join(" ", Keys.Select(Loc.T)).ToLowerInvariant();

                    foreach (var forbidden in new[] { "hire", "hirable", "hireable", "team screen", "zatrudni" })
                    {
                        Assert.That(copy, Does.Not.Contain(forbidden),
                            $"the card promises the hiring tier in {language}, and the game cannot deliver it yet");
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }

        [Test]
        public void ThereIsOneButtonAndItIsTheOnlyWayOut()
        {
            var card = SupporterCard.Build();
            var buttons = card.Query<Button>().ToList();

            Assert.That(buttons, Has.Count.EqualTo(1),
                "a notice with two calls to action is an advertisement");
            Assert.That(buttons[0].text, Is.EqualTo(Loc.T("support.open")));
        }

        /// <summary>
        /// The counter on the site reads `docs/assets/data/steam-fund.json`, which points at the
        /// same address. Two addresses for one fund is the disagreement with a date on it.
        /// </summary>
        [Test]
        public void TheAddressIsTheOneTheSiteCounterPointsAt()
        {
            Assert.That(SupporterCard.Url, Is.EqualTo("https://github.com/sponsors/HuckleR2003"));
        }

        [Test]
        public void TheCornerVariantIsTheSameCardWithOneClassMore()
        {
            var plain = SupporterCard.Build();
            var corner = SupporterCard.Build(corner: true);

            Assert.That(plain.ClassListContains("support-card--corner"), Is.False);
            Assert.That(corner.ClassListContains("support-card--corner"), Is.True);
            Assert.That(corner.ClassListContains("support-card"), Is.True);
        }
    }
}
