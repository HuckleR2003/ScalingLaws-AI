using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The thread with Emil.
    ///
    /// **The claim worth testing is that it is a record and not a calculation.** What he says about
    /// his own company is his share price against where it stood three months earlier, read on the
    /// day he says it. If that were re-derived on load, a message sent in the first year would
    /// silently become an opinion about the tenth, and nobody would ever notice.
    /// </summary>
    public sealed class MessengerTests
    {
        /// <summary>The day is the campaign day, counting the first as one rather than as zero.</summary>
        [Test]
        public void EveryMessageKnowsTheDayItWasSent()
        {
            var thread = new Messenger();

            Assert.IsTrue(thread.IsEmpty);

            thread.Say(new GameDate(0), true, "first");
            thread.Say(new GameDate(411), false, "later");

            Assert.AreEqual(2, thread.Count);
            Assert.AreEqual(1, thread.Lines[0].Day, "Day zero would read as no day at all.");
            Assert.AreEqual(412, thread.Lines[1].Day);

            Assert.IsTrue(thread.Lines[0].Mine);
            Assert.IsFalse(thread.Lines[1].Mine);

            // Nothing is a message. An empty bubble is a rendering fault wearing a conversation.
            thread.Say(new GameDate(5), true, "   ");
            Assert.AreEqual(2, thread.Count);
        }

        /// <summary>
        /// A long campaign does not carry an unbounded thread, and the newest survive.
        /// </summary>
        [Test]
        public void TheOldestFallOffAndTheNewestStay()
        {
            var thread = new Messenger();

            for (var index = 0; index < Messenger.MostKept * 2; index++)
            {
                thread.Say(new GameDate(index), index % 2 == 0, "line " + index);
            }

            Assert.AreEqual(Messenger.MostKept, thread.Count);

            Assert.AreEqual("line " + (Messenger.MostKept * 2 - 1),
                thread.Lines[thread.Count - 1].Text,
                "The newest message was the one dropped, which is the wrong way round.");
        }

        /// <summary>
        /// The words survive a save exactly as they were said.
        ///
        /// **Not the key, the text.** Storing a key and resolving it on load would replay an old
        /// message with today's numbers in it, which is the whole reason this is persisted at all.
        /// </summary>
        [Test]
        public void WhatHeSaidIsWhatComesBack()
        {
            var state = new CompanyState("Adco", 12);

            state.Messages.Say(state.Date, true, "How is business?");
            state.Messages.Say(state.Date, false, "Share price is up 12% on the quarter, ninth place.");

            var json = UnityEngine.JsonUtility.ToJson(SaveStore.Capture(state));
            var restored = SaveStore.Restore(SaveStore.Parse(json));

            Assert.AreEqual(2, restored.Messages.Count, "The thread was lost on load.");

            Assert.AreEqual("Share price is up 12% on the quarter, ninth place.",
                restored.Messages.Lines[1].Text);

            Assert.IsFalse(restored.Messages.Lines[1].Mine);
            Assert.AreEqual(state.Date.DayIndex + 1, restored.Messages.Lines[1].Day);
        }

        /// <summary>A v47 file has no thread, and inventing one would mean inventing its figures.</summary>
        [Test]
        public void AnOlderSaveArrivesWithAnEmptyThread()
        {
            var upgraded = SaveMigration.UpgradeV47ToV48(new SaveData { version = 47 });

            Assert.AreEqual(48, upgraded.version);
            CollectionAssert.IsEmpty(upgraded.messages);
        }

        /// <summary>The messenger's own words exist in both languages.</summary>
        [Test]
        public void TheMessengerSpeaksBothLanguages()
        {
            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var key in new[]
                    {
                        "phone.menu.messenger", "phone.thread.empty", "phone.compose.write",
                        "phone.compose.guides", "phone.guide.now", "phone.guide.yes",
                        "phone.guide.later", "phone.guide.no"
                    })
                    {
                        Assert.AreNotEqual(key, Loc.T(key),
                            $"{key} has no words in {language}, so the phone prints its own key.");
                    }

                    // The two that take a value have to actually place it.
                    StringAssert.Contains("412", Loc.T("phone.day", "412"));
                    StringAssert.Contains("Serwerownia",
                        Loc.T("phone.ask.guide", "Serwerownia"));
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }
    }
}
