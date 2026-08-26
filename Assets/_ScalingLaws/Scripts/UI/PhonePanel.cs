using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The phone in the corner: it wakes, opens an app, and Emil is on the other end of it.
    ///
    /// **Everything is scheduled rather than coroutined**, because a UI Toolkit element can carry
    /// its own schedule and dies with it. A coroutine on a MonoBehaviour outlives the panel it was
    /// animating, and the first time the player changes screen mid-boot it starts writing into an
    /// element that has left the tree.
    ///
    /// The sequence is deliberately unhurried at the start and quick afterwards: the boot is the
    /// one moment the game asks the player to just watch, and everything after it is a conversation
    /// they are having. Roughly seven seconds from black screen to the first choice.
    /// </summary>
    public sealed class PhonePanel
    {
        // ---- the timings, in milliseconds -------------------------------------------------------

        /// <summary>Black screen before the display lights.</summary>
        public const int WakeDelay = 450;

        /// <summary>The screen fading up.</summary>
        public const int ScreenFade = 700;

        /// <summary>How long the app icon sits as a square before it starts to change.</summary>
        public const int IconHold = 900;

        /// <summary>The square becoming three circles.</summary>
        public const int IconMorph = 800;

        /// <summary>The circles shimmering while it loads.</summary>
        public const int LoadingHold = 1100;

        /// <summary>Before the menu appears.</summary>
        public const int WelcomeHold = 700;

        /// <summary>Before Messages lights up on its own.</summary>
        public const int AutoSelectDelay = 1000;

        /// <summary>Before the chat replaces the menu.</summary>
        public const int OpenChatDelay = 620;

        /// <summary>How long the phone takes to roll up into the corner banner.</summary>
        public const int CollapseMilliseconds = 520;

        private readonly VisualElement host;
        private readonly Action<bool> answered;

        private VisualElement frame;
        private VisualElement screen;
        private VisualElement app;
        private VisualElement chatList;
        private VisualElement choices;

        public PhonePanel(VisualElement host, Action<bool> answered)
        {
            this.host = host;
            this.answered = answered;
        }

        /// <summary>True while the phone is on screen and has not been answered.</summary>
        public bool IsOpen => frame != null && frame.parent != null;

        /// <summary>True when this is him ringing back rather than the opening call.</summary>
        private bool returning;

        /// <summary>
        /// Puts the phone on screen and starts the sequence.
        ///
        /// Nothing here waits on the player until the two reply buttons appear, which is the point
        /// at which they are given something to decide.
        /// </summary>
        public void Ring(bool callingBack = false)
        {
            Close();

            // **Which conversation this is.** He introduced himself the first time; doing it again
            // when the player asked him to call back would say the tour was starting over, which is
            // the one thing this call is not.
            returning = callingBack;

            frame = new VisualElement();
            frame.AddToClassList("phone");

            // The handset itself. If the art is missing the frame still reads as a phone, because
            // the styling draws the body — the picture is the finish, not the shape.
            var art = Resources.Load<Texture2D>("Others/phone");

            if (art != null)
            {
                frame.style.backgroundImage = new StyleBackground(art);
                frame.AddToClassList("phone--art");
            }

            screen = new VisualElement();
            screen.AddToClassList("phone__screen");
            frame.Add(screen);

            host.Add(frame);

            // Born small and low, released a frame later so the arrival has somewhere to move from.
            frame.AddToClassList("phone--arriving");
            frame.schedule.Execute(() => frame.RemoveFromClassList("phone--arriving"))
                .ExecuteLater(16);

            frame.schedule.Execute(LightUp).ExecuteLater(WakeDelay);
        }

        /// <summary>The display coming on, then the app opening itself.</summary>
        private void LightUp()
        {
            screen.AddToClassList("phone__screen--on");
            screen.schedule.Execute(ShowApp).ExecuteLater(ScreenFade);
        }

        /// <summary>
        /// The blue square with the app's name on it, which becomes three circles.
        ///
        /// The morph is a border-radius and a scale rather than a sprite swap: a square that
        /// rounds itself into a disc and splits is the cheapest convincing thing UI Toolkit can do,
        /// and it does not need art nobody has drawn yet.
        /// </summary>
        private void ShowApp()
        {
            screen.Clear();

            app = new VisualElement();
            app.AddToClassList("dinapp");
            screen.Add(app);

            var mark = new VisualElement();
            mark.AddToClassList("dinapp__mark");

            var name = new Label(GuideScript.AppName);
            name.AddToClassList("dinapp__name");
            mark.Add(name);

            app.Add(mark);

            app.schedule.Execute(() =>
            {
                // The square opens out. The label goes first, because three loading circles with a
                // word across them reads as a bug.
                name.AddToClassList("dinapp__name--gone");
                mark.AddToClassList("dinapp__mark--morphing");

                mark.schedule.Execute(() =>
                {
                    mark.style.display = DisplayStyle.None;

                    var dots = new VisualElement();
                    dots.AddToClassList("dinapp__dots");

                    for (var index = 0; index < 3; index++)
                    {
                        var dot = new VisualElement();
                        dot.AddToClassList("dinapp__dot");
                        dots.Add(dot);

                        // Staggered, so they shimmer in sequence rather than pulsing as one lump.
                        var step = index;
                        dot.schedule.Execute(() => dot.ToggleInClassList("dinapp__dot--lit"))
                            .Every(280).StartingIn(step * 140);
                    }

                    app.Add(dots);
                    app.schedule.Execute(ShowWelcome).ExecuteLater(LoadingHold);
                }).ExecuteLater(IconMorph);
            }).ExecuteLater(IconHold);
        }

        /// <summary>"Welcome back!" and the four things the app does.</summary>
        private void ShowWelcome()
        {
            screen.Clear();

            var welcome = new Label(GuideScript.WelcomeLine);
            welcome.AddToClassList("dinapp__welcome");
            screen.Add(welcome);

            var menu = new VisualElement();
            menu.AddToClassList("dinapp__menu");

            var rows = new List<VisualElement>();

            for (var index = 0; index < GuideScript.AppMenu.Count; index++)
            {
                var row = new VisualElement();
                row.AddToClassList("dinapp__row");

                var number = new Label($"{index + 1}");
                number.AddToClassList("dinapp__rownumber");
                row.Add(number);

                var label = new Label(GuideScript.AppMenu[index]);
                label.AddToClassList("dinapp__rowlabel");
                row.Add(label);

                menu.Add(row);
                rows.Add(row);
            }

            screen.Add(menu);

            // Messages lights up on its own, as if somebody picked it. The player is watching
            // somebody else's phone unlock, which is a nicer way in than a button that says START.
            screen.schedule.Execute(() =>
            {
                rows[GuideScript.AutoSelectedMenuItem].AddToClassList("dinapp__row--picked");
                screen.schedule.Execute(ShowChat).ExecuteLater(OpenChatDelay);
            }).ExecuteLater(AutoSelectDelay);
        }

        // ---- the conversation ---------------------------------------------------------------------

        private void ShowChat()
        {
            screen.Clear();

            var header = new VisualElement();
            header.AddToClassList("chat__header");

            header.Add(Avatar(34));

            var who = new VisualElement();
            who.AddToClassList("chat__who");

            var handle = new Label(GuideScript.CousinHandle);
            handle.AddToClassList("chat__handle");
            who.Add(handle);

            var status = new Label("online");
            status.AddToClassList("chat__status");
            who.Add(status);

            header.Add(who);
            screen.Add(header);

            chatList = new ScrollView();
            chatList.AddToClassList("chat__list");
            screen.Add(chatList);

            foreach (var line in returning ? GuideScript.ReturnBacklog : GuideScript.Backlog)
            {
                chatList.Add(Bubble(line, false));
            }

            // Then he starts typing, which is what turns a wall of text into a conversation.
            var delay = 0f;

            foreach (var (pause, typing, text) in returning ? GuideScript.ReturnLive : GuideScript.Live)
            {
                delay += pause;

                var showTypingAt = delay;
                var showTextAt = delay + typing;
                var message = text;

                screen.schedule.Execute(() => ShowTyping(true))
                    .ExecuteLater((long)(showTypingAt * 1000f));

                screen.schedule.Execute(() =>
                {
                    ShowTyping(false);
                    chatList.Add(Bubble(message, false));
                    ScrollDown();
                }).ExecuteLater((long)(showTextAt * 1000f));

                delay = showTextAt;
            }

            screen.schedule.Execute(ShowChoices).ExecuteLater((long)(delay * 1000f) + 500);
        }

        private VisualElement typing;

        /// <summary>The three-dot bubble. One at a time, and always removed before a message lands.</summary>
        private void ShowTyping(bool on)
        {
            typing?.RemoveFromHierarchy();
            typing = null;

            if (!on || chatList == null)
            {
                return;
            }

            typing = new VisualElement();
            typing.AddToClassList("chat__bubble");
            typing.AddToClassList("chat__bubble--them");
            typing.AddToClassList("chat__typing");

            for (var index = 0; index < 3; index++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("chat__typingdot");
                typing.Add(dot);

                var step = index;
                dot.schedule.Execute(() => dot.ToggleInClassList("chat__typingdot--up"))
                    .Every(300).StartingIn(step * 150);
            }

            chatList.Add(typing);
            ScrollDown();
        }

        /// <summary>The two things the player can send back.</summary>
        private void ShowChoices()
        {
            choices = new VisualElement();
            choices.AddToClassList("chat__choices");

            var accept = new Button(() => Answer(true))
            {
                text = returning ? GuideScript.ReturnAccept : GuideScript.ReplyAccept
            };
            accept.AddToClassList("chat__choice");
            accept.AddToClassList("chat__choice--yes");
            choices.Add(accept);

            var decline = new Button(() => Answer(false)) { text = GuideScript.ReplyDecline };
            decline.AddToClassList("chat__choice");
            choices.Add(decline);

            screen.Add(choices);
        }

        /// <summary>
        /// Sends the reply, shows it, and gets the phone out of the way.
        ///
        /// Declining gets one more line from him first, because a refusal that is answered with
        /// nothing reads as the game sulking.
        /// </summary>
        private void Answer(bool accepted)
        {
            choices?.RemoveFromHierarchy();
            choices = null;

            chatList.Add(Bubble(accepted ? GuideScript.ReplyAccept : GuideScript.ReplyDecline, true));
            ScrollDown();

            if (accepted)
            {
                screen.schedule.Execute(() => Collapse(true)).ExecuteLater(900);
                return;
            }

            screen.schedule.Execute(() => ShowTyping(true)).ExecuteLater(700);

            screen.schedule.Execute(() =>
            {
                ShowTyping(false);
                chatList.Add(Bubble(GuideScript.DeclineReply, false));
                ScrollDown();
            }).ExecuteLater(2300);

            screen.schedule.Execute(() => Collapse(false)).ExecuteLater(4200);
        }

        /// <summary>
        /// The phone rolls up into the corner and the task banner takes over.
        ///
        /// A transform rather than a fade: the author asked for it to visibly become the banner, so
        /// it shrinks towards the top right and hands over at the end of the move rather than
        /// dissolving and having something else appear.
        /// </summary>
        private void Collapse(bool accepted)
        {
            if (frame == null)
            {
                return;
            }

            frame.AddToClassList("phone--collapsing");

            frame.schedule.Execute(() =>
            {
                Close();
                answered?.Invoke(accepted);
            }).ExecuteLater(CollapseMilliseconds);
        }

        public void Close()
        {
            typing = null;
            choices = null;
            chatList = null;
            app = null;
            screen = null;

            frame?.RemoveFromHierarchy();
            frame = null;
        }

        // ---- pieces ------------------------------------------------------------------------------

        /// <summary>
        /// Emil's face, or his initial.
        ///
        /// The portrait studio is used when the character pack is installed, and an initial on a
        /// coloured disc when it is not — same rule the hiring screens follow, because the pack is
        /// gitignored and a fresh clone has none of it.
        /// </summary>
        public static VisualElement Avatar(int size)
        {
            var circle = new VisualElement();
            circle.AddToClassList("avatar");
            circle.style.width = size;
            circle.style.height = size;

            var face = CousinFace();

            if (face != null)
            {
                circle.style.backgroundImage = new StyleBackground(face);
                return circle;
            }

            var initial = new Label(GuideScript.CousinName[..1]);
            initial.AddToClassList("avatar__initial");
            circle.Add(initial);

            return circle;
        }

        private static Texture2D cousinFace;
        private static bool cousinTried;

        /// <summary>Rendered once. He is the same person every time he writes.</summary>
        private static Texture2D CousinFace()
        {
            if (cousinTried)
            {
                return cousinFace;
            }

            cousinTried = true;

            var studio = new PortraitStudio();

            if (!studio.Open())
            {
                studio.Close();
                return null;
            }

            // A fixed look, so he is recognisably one person rather than whoever the studio
            // happened to load first.
            studio.StepLook(2 - studio.LookIndex);
            studio.StepGlasses(-studio.GlassesIndex);
            studio.RenderNow();

            if (studio.Texture != null)
            {
                var wasActive = RenderTexture.active;
                RenderTexture.active = studio.Texture;

                cousinFace = new Texture2D(studio.Texture.width, studio.Texture.height,
                    TextureFormat.RGBA32, false);

                cousinFace.ReadPixels(new Rect(0, 0, studio.Texture.width, studio.Texture.height),
                    0, 0);

                cousinFace.Apply();
                RenderTexture.active = wasActive;
            }

            studio.Close();
            return cousinFace;
        }

        private static VisualElement Bubble(string text, bool mine)
        {
            var bubble = new VisualElement();
            bubble.AddToClassList("chat__bubble");
            bubble.AddToClassList(mine ? "chat__bubble--me" : "chat__bubble--them");

            var label = new Label(text);
            label.AddToClassList("chat__text");
            bubble.Add(label);

            // Born flat and released a frame later, so each message arrives rather than appearing.
            bubble.AddToClassList("chat__bubble--arriving");
            bubble.schedule.Execute(() => bubble.RemoveFromClassList("chat__bubble--arriving"))
                .ExecuteLater(16);

            return bubble;
        }

        private void ScrollDown()
        {
            if (chatList is ScrollView view)
            {
                // Deferred a frame: the new bubble has not been laid out yet, so the scroller has
                // no range to move within until the next pass.
                view.schedule.Execute(() =>
                    view.scrollOffset = new Vector2(0f, float.MaxValue)).ExecuteLater(1);
            }
        }
    }
}
