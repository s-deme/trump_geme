#nullable enable

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductPresentationTests
    {
        [Test]
        public void FeedbackCatalogHasOneStableNonColorEntryPerKind()
        {
            ProductFeedbackKind[] kinds = Enum.GetValues(typeof(ProductFeedbackKind))
                .Cast<ProductFeedbackKind>().ToArray();

            Assert.That(ProductPresentationCatalog.All.Select(entry => entry.Kind),
                Is.EqualTo(kinds));
            Assert.That(ProductPresentationCatalog.All.Select(entry => entry.Key).Distinct().Count(),
                Is.EqualTo(kinds.Length));
            Assert.That(ProductPresentationCatalog.All.All(entry =>
                entry.Key.StartsWith("feedback.", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(entry.Symbol) &&
                !string.IsNullOrWhiteSpace(entry.EnglishFallback) &&
                entry.DisplayText.Contains(entry.Symbol) &&
                entry.DisplayText.Contains(entry.EnglishFallback)), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ProductPresentationCatalog.Get((ProductFeedbackKind)999));
        }

        [Test]
        public void PresentationPolicyChangesOnlyTimingAndStopsReducedMotion()
        {
            ProductPresentationPolicy reduced = ProductPresentationPolicy.From(
                ProductPresentationSpeed.Reduced, reducedMotion: false);
            ProductPresentationPolicy normal = ProductPresentationPolicy.From(
                ProductPresentationSpeed.Normal, reducedMotion: false);
            ProductPresentationPolicy fast = ProductPresentationPolicy.From(
                ProductPresentationSpeed.Fast, reducedMotion: false);
            ProductPresentationPolicy accessible = ProductPresentationPolicy.From(
                ProductPresentationSpeed.Normal, reducedMotion: true);

            Assert.That(reduced.MotionEnabled, Is.False);
            Assert.That(reduced.CueEnterSeconds, Is.Zero);
            Assert.That(reduced.CueExitSeconds, Is.Zero);
            Assert.That(reduced.TransitionSeconds, Is.Zero);
            Assert.That(reduced.CueHoldSeconds, Is.GreaterThan(0f));

            Assert.That(normal.MotionEnabled, Is.True);
            Assert.That(fast.MotionEnabled, Is.True);
            Assert.That(fast.CueEnterSeconds, Is.LessThan(normal.CueEnterSeconds));
            Assert.That(fast.CueHoldSeconds, Is.LessThan(normal.CueHoldSeconds));
            Assert.That(fast.CueExitSeconds, Is.LessThan(normal.CueExitSeconds));
            Assert.That(fast.TransitionSeconds, Is.LessThan(normal.TransitionSeconds));

            Assert.That(accessible.MotionEnabled, Is.False);
            Assert.That(accessible.CueEnterSeconds, Is.Zero);
            Assert.That(accessible.CueExitSeconds, Is.Zero);
            Assert.That(accessible.TransitionSeconds, Is.Zero);
            Assert.That(accessible.CueHoldSeconds, Is.EqualTo(normal.CueHoldSeconds));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ProductPresentationPolicy.From((ProductPresentationSpeed)999,
                    reducedMotion: false));
        }

        [Test]
        public void ControllerPresentsStaticCueAndDelegatesAudioExactlyOnce()
        {
            GameObject root = new GameObject("PresentationTestRoot");
            root.SetActive(false);
            try
            {
                ProductPresentationController controller =
                    root.AddComponent<ProductPresentationController>();
                CanvasGroup banner = Child<CanvasGroup>(root.transform, "Banner");
                Image bannerImage = banner.gameObject.AddComponent<Image>();
                Text bannerText = Child<Text>(banner.transform, "Text");
                CanvasGroup transition = Child<CanvasGroup>(root.transform, "Transition");
                var audio = new RecordingAudioFeedback();
                controller.Configure(banner, bannerImage, bannerText, transition, audio);
                root.SetActive(true);

                ProductSettings settings = ProductSettings.CreateDefaults()
                    .WithPresentationSpeed(ProductPresentationSpeed.Reduced);
                controller.ApplySettings(settings);
                int cueEvents = 0;
                controller.CuePresented += kind =>
                {
                    Assert.That(kind, Is.EqualTo(ProductFeedbackKind.Error));
                    cueEvents++;
                };

                controller.Play(ProductFeedbackKind.Error);

                Assert.That(controller.LastKind, Is.EqualTo(ProductFeedbackKind.Error));
                Assert.That(controller.LastKey, Is.EqualTo("feedback.error"));
                Assert.That(controller.BannerText.text, Does.Contain("!"));
                Assert.That(controller.BannerText.text, Does.Contain("Error"));
                Assert.That(controller.Banner.alpha, Is.EqualTo(1f));
                Assert.That(controller.Banner.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(audio.ApplyCount, Is.EqualTo(1));
                Assert.That(audio.PlayCount, Is.EqualTo(1));
                Assert.That(audio.LastKind, Is.EqualTo(ProductFeedbackKind.Error));
                Assert.That(cueEvents, Is.EqualTo(1));

                int transitions = 0;
                controller.TransitionCompleted += () => transitions++;
                controller.BeginScreenTransition();
                Assert.That(controller.IsTransitioning, Is.False);
                Assert.That(controller.Transition.alpha, Is.Zero);
                Assert.That(controller.Transition.blocksRaycasts, Is.False);
                Assert.That(transitions, Is.EqualTo(1));

                root.SetActive(false);
                controller.Cancel();
                Assert.That(controller.Banner.alpha, Is.Zero);
                Assert.That(controller.Transition.alpha, Is.Zero);
                Assert.That(controller.Transition.blocksRaycasts, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResultOutcomeIsStructuredAndScreenUsesTextSymbolAndPalette()
        {
            ResultViewModel win = CrazyEightsResultPresenter.Create(new GameResultPresentation(
                winners: new[] { 0 }, scores: new[] { 5d, -5d },
                reason: "empty hand", turns: 9));
            ResultViewModel loss = CrazyEightsResultPresenter.Create(new GameResultPresentation(
                winners: new[] { 1 }, scores: new[] { -5d, 5d },
                reason: "empty hand", turns: 9));
            ResultViewModel draw = CrazyEightsResultPresenter.Create(new GameResultPresentation(
                winners: Array.Empty<int>(), scores: new[] { 0d, 0d },
                reason: "draw", turns: 9));

            Assert.That(win.Outcome, Is.EqualTo(ProductResultOutcome.Win));
            Assert.That(loss.Outcome, Is.EqualTo(ProductResultOutcome.Loss));
            Assert.That(draw.Outcome, Is.EqualTo(ProductResultOutcome.Draw));

            GameObject root = new GameObject("ResultScreenTestRoot");
            root.SetActive(false);
            try
            {
                ResultScreen screen = root.AddComponent<ResultScreen>();
                Text summary = Child<Text>(root.transform, "Summary");
                Button details = Child<Button>(root.transform, "Details");
                Button rematch = Child<Button>(root.transform, "Rematch");
                Button title = Child<Button>(root.transform, "Title");
                screen.Configure(summary, details, rematch, title);

                screen.Render(win);
                Color winColor = summary.color;
                Assert.That(screen.LastOutcome, Is.EqualTo(ProductResultOutcome.Win));
                Assert.That(summary.text, Does.StartWith("★ WIN"));
                Assert.That(summary.text, Does.Contain("You win!"));

                screen.Render(loss);
                Color lossColor = summary.color;
                Assert.That(screen.LastOutcome, Is.EqualTo(ProductResultOutcome.Loss));
                Assert.That(summary.text, Does.StartWith("◆ LOSS"));
                Assert.That(summary.text, Does.Contain("CPU wins"));
                Assert.That(lossColor, Is.Not.EqualTo(winColor));

                screen.Render(draw);
                Assert.That(screen.LastOutcome, Is.EqualTo(ProductResultOutcome.Draw));
                Assert.That(summary.text, Does.StartWith("= DRAW"));
                Assert.That(summary.color, Is.Not.EqualTo(lossColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static T Child<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.AddComponent<T>();
        }

        private sealed class RecordingAudioFeedback : IProductAudioFeedback
        {
            public int ApplyCount { get; private set; }
            public int PlayCount { get; private set; }
            public ProductFeedbackKind? LastKind { get; private set; }

            public void ApplySettings(ProductSettings settings)
            {
                Assert.That(settings, Is.Not.Null);
                ApplyCount++;
            }

            public void Play(ProductFeedbackKind kind)
            {
                LastKind = kind;
                PlayCount++;
            }
        }
    }
}
