using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Code that exists and is never called.
    ///
    /// **This has now happened five times in this project and every one of them shipped.** The model
    /// type was chosen everywhere and passed nowhere. `Retire` could only be reached by a safety
    /// incident. Debt had a full simulation and no button. Thirty four kinds of event were raised
    /// and drained into a list nothing read. And `ShowResearchCard`, the popup with the icon, the
    /// description, the costs and the start button, sat complete and uncalled while clicking a
    /// research node only moved a ring.
    ///
    /// Every one of those passed the whole suite, because a test that drives the simulation cannot
    /// tell the difference between a control that is missing and one that is merely unreachable.
    /// This fixture reads the source instead. It is crude on purpose: a private method nobody calls
    /// is either dead or unwired, and both are worth a failure.
    /// </summary>
    public sealed class UiWiringTests
    {
        private static string UiFolder =>
            Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts", "UI");

        private static Dictionary<string, string> UiSources()
        {
            var sources = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(UiFolder, "*.cs", SearchOption.AllDirectories))
            {
                sources[Path.GetFileName(file)] = File.ReadAllText(file);
            }

            return sources;
        }

        /// <summary>
        /// Every private method in the interface layer is called by something.
        ///
        /// Private is the right scope to check. A public method with no caller may be an entry point
        /// for a test or for a screen not written yet; a private one with no caller cannot be reached
        /// by anything, ever, and the compiler does not warn about it when it is invoked nowhere but
        /// looks used because a method of the same shape exists.
        /// </summary>
        [Test]
        public void NoPrivateMethodInTheInterfaceIsUnreachable()
        {
            var sources = UiSources();
            Assert.IsNotEmpty(sources, "No UI sources found, so this test is checking nothing.");

            var declaration = new Regex(
                @"private\s+(?:static\s+)?(?:readonly\s+)?[\w<>\[\],\.\?]+\s+(\w+)\s*\(",
                RegexOptions.Compiled);

            var orphans = new List<string>();

            // A partial type is spread across several files, so "declared here and never mentioned
            // here" stopped meaning "unreachable" the moment GameShell was split. The unit of
            // search is the type, not the file: everything declaring the same partial class is
            // searched together.
            //
            // Narrowing this to the type rather than to the whole folder matters. Searching every
            // UI file would let an identically named method in an unrelated class vouch for a dead
            // one, and this guard has paid for itself six times precisely by not doing that.
            var partOf = PartnerText(sources);

            foreach (var (fileName, text) in sources)
            {
                var searchable = partOf[fileName];

                foreach (Match match in declaration.Matches(text))
                {
                    var name = match.Groups[1].Value;

                    // Unity calls these itself, and a constructor shares its name with its type.
                    if (name is "Update" or "OnEnable" or "OnDisable" or "OnDestroy" or "Awake"
                        or "Start" or "OnGUI" or "if" or "for" or "foreach" or "while" or "switch"
                        or "return" or "catch" or "using" or "lock" or "readonly")
                    {
                        continue;
                    }

                    if (name == Path.GetFileNameWithoutExtension(fileName))
                    {
                        continue;
                    }

                    // The bare identifier, not "name(". Half the interface is wired with method
                    // groups: generateVisualContent += Draw, new Button(SkipDay), a callback passed
                    // by name. Counting only call syntax reported every one of those as dead, which
                    // is the kind of false alarm that gets a test deleted rather than fixed.
                    var uses = Regex.Matches(searchable, @"\b" + Regex.Escape(name) + @"\b").Count;
                    var declarations = Regex.Matches(
                        searchable, @"private\s+(?:static\s+)?[\w<>\[\],\.\?]+\s+"
                              + Regex.Escape(name) + @"\s*\(").Count;

                    if (uses <= declarations)
                    {
                        orphans.Add($"{fileName}: {name}");
                    }
                }
            }

            Assert.IsEmpty(orphans,
                "These are written and never called, so they are either dead or a control the player "
                + "cannot reach:\n  " + string.Join("\n  ", orphans)
                + "\n\nThis has shipped five times. Delete it or wire it.");
        }


        /// <summary>
        /// The text every part of a type is spread across, keyed by file.
        ///
        /// A partial class is one type in several files, so "declared here and never mentioned
        /// here" stopped being evidence of anything the moment `GameShell` was split. Grouping by
        /// the declared type keeps the guard as strict as it was: it is still not enough for a
        /// method to be mentioned somewhere in the folder, only somewhere in its own class.
        /// </summary>
        private static Dictionary<string, string> PartnerText(Dictionary<string, string> sources)
        {
            // Anchored to a real declaration line, with its modifiers. Matching the bare word
            // "class" found it first inside a doc comment ("the failure class is ...") and
            // grouped the two halves of one type under two invented names, which silently
            // turned this guard off while leaving it green.
            var declared = new Regex(
                @"^\s*(?:public|internal|private|protected)[\w\s]*?\bclass\s+(\w+)",
                RegexOptions.Compiled | RegexOptions.Multiline);
            var byType = new Dictionary<string, string>();
            var typeOf = new Dictionary<string, string>();

            foreach (var (fileName, text) in sources)
            {
                var match = declared.Match(text);
                var type = match.Success ? match.Groups[1].Value : fileName;

                typeOf[fileName] = type;
                byType[type] = byType.TryGetValue(type, out var alreadySeen) ? alreadySeen + text : text;
            }

            var result = new Dictionary<string, string>();

            foreach (var (fileName, type) in typeOf)
            {
                result[fileName] = byType[type];
            }

            return result;
        }

        /// <summary>
        /// The specific one that just cost a turn: the research popup has to be opened by something.
        /// </summary>
        [Test]
        public void ClickingAResearchNodeOpensTheCard()
        {
            // Read across the whole partial type. The declaration is in GameShell.Research.cs and
            // the click that opens it is in GameShell.cs, so naming one file finds one half.
            var shell = string.Concat(UiSources()
                .Where(entry => entry.Key.StartsWith("GameShell", StringComparison.Ordinal))
                .Select(entry => entry.Value));

            var calls = Regex.Matches(shell, @"ShowResearchCard\s*\(").Count;

            Assert.Greater(calls, 1,
                "ShowResearchCard is declared and never called. Clicking a node then only moves a "
                + "ring, and the player is left to guess what the node does.");

            Assert.IsTrue(shell.Contains("RegisterCallback<ClickEvent>"),
                "The card has to open where the cursor is, which needs the click event rather than "
                + "the button action.");
        }

        /// <summary>
        /// Both corner banners have to be hidden the moment the screen changes rather than on the
        /// next tick, because a paused game has no next tick and that is when a player reads.
        /// </summary>
        [Test]
        public void TheCornerBannersAreHiddenWhenTheScreenChangesRatherThanOnTheNextDay()
        {
            var shell = UiSources()["GameShell.cs"];

            var show = shell.IndexOf("private void Show(Screen screen)", System.StringComparison.Ordinal);
            Assert.Greater(show, 0, "Show(Screen) is gone, so this test is checking nothing.");

            // **The whole method, matched brace to brace, not the first 2,200 characters of it.**
            //
            // The window was a fixed byte count, so this went red the day a comment was added near
            // the top of `Show` and the line it looks for slid past the end of the window. Nothing
            // about the game had changed. A test that fails on comment length is measuring the
            // wrong thing, and widening the number would only move the day it happens again.
            var body = MethodBody(shell, show);

            Assert.IsTrue(body.Contains("newsBanner?.SetHidden"),
                "The news banner is not hidden inside Show, so it stays over the new screen until a "
                + "day rolls over, and while paused no day rolls over.");

            Assert.IsTrue(body.Contains("modelBanner?.SetHidden"),
                "Same for the product banner.");
        }

        /// <summary>
        /// One method's source, from its signature to the brace that closes it.
        ///
        /// Braces inside strings and character literals are skipped, because `"{0}"` appears in this
        /// codebase constantly and one of them would end the method early and silently.
        /// </summary>
        private static string MethodBody(string source, int signatureAt)
        {
            var open = source.IndexOf('{', signatureAt);
            Assert.Greater(open, signatureAt, "No body found after the signature.");

            var depth = 0;

            for (var index = open; index < source.Length; index++)
            {
                var character = source[index];

                // **Comments first, and that is not fussiness.** An apostrophe in a comment, and
                // this codebase is full of them, opened a character literal that then swallowed
                // every brace until the next apostrophe, and the scan reported that the method
                // never closed. Found the day such a comment was added to `Show`.
                if (character == '/' && index + 1 < source.Length)
                {
                    if (source[index + 1] == '/')
                    {
                        while (index < source.Length && source[index] != '\n')
                        {
                            index++;
                        }

                        continue;
                    }

                    if (source[index + 1] == '*')
                    {
                        index += 2;

                        while (index + 1 < source.Length
                            && !(source[index] == '*' && source[index + 1] == '/'))
                        {
                            index++;
                        }

                        index++;
                        continue;
                    }
                }

                if (character == '"' || character == '\'')
                {
                    var quote = character;
                    index++;

                    while (index < source.Length && source[index] != quote)
                    {
                        index += source[index] == '\\' ? 2 : 1;
                    }

                    continue;
                }

                if (character == '{')
                {
                    depth++;
                }
                else if (character == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        return source.Substring(signatureAt, index - signatureAt + 1);
                    }
                }
            }

            Assert.Fail("The method never closed, so the source did not parse the way this expects.");
            return string.Empty;
        }
    }
}
