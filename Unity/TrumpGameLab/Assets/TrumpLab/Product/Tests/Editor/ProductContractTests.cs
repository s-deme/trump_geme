#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductContractTests
    {
        [Test]
        public void SettingsCreateValidatedImmutableRequest()
        {
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "-17", "8", CpuDifficulties.Hard,
                out GameStartRequest? request, out string error), Is.True, error);
            Assert.That(request, Is.EqualTo(new GameStartRequest(
                -17, 8, CpuDifficulties.Hard)));
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "-17", "8", out request, out error), Is.True, error);
            Assert.That(request!.Difficulty, Is.EqualTo(CpuDifficulties.Standard));
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "nope", "8", CpuDifficulties.Standard, out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "1", "0", CpuDifficulties.Standard, out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "1", "14", CpuDifficulties.Standard, out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "1", "8", 99, out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameStartRequest(1, 8, difficulty: 99));
        }

        [Test]
        public void MatchPresenterUsesStructuredActionsAndHidesOpponentCards()
        {
            IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed: 1);
            var provider = (IGamePresentationProvider)game;
            GamePresentation presentation = provider.Present(viewer: 0);
            MatchViewModel model = CrazyEightsMatchPresenter.Create(presentation, inputEnabled: true);

            Assert.That(model.InputEnabled, Is.EqualTo(presentation.CurrentPlayer == 0));
            Assert.That(model.Actions.Select(action => action.Id),
                Is.EqualTo(presentation.Actions.Select(action => action.Id)));
            Assert.That(model.Actions.All(action => !string.IsNullOrWhiteSpace(action.Label)), Is.True);
            Assert.That(model.Actions.All(action => !string.IsNullOrWhiteSpace(action.Reason)), Is.True);
            Assert.That(model.ContextHelp, Does.Contain("Every shown action is legal"));
            Assert.That(model.ActionSummary, Does.Contain("legal action"));
            Assert.That(model.OpponentHand, Does.StartWith("CPU hand: "));
            Assert.That(model.OpponentHand, Does.Not.Contain("♣"));
            Assert.That(model.OpponentHand, Does.Not.Contain("♦"));
            Assert.That(model.OpponentHand, Does.Not.Contain("♥"));
            Assert.That(model.OpponentHand, Does.Not.Contain("♠"));
        }

        [Test]
        public void SessionRejectsStaleInputAndCompletesWithHumanAndCpuActions()
        {
            var session = new GameSessionController(seed: 1);
            session.Begin();
            int humanActions = 0;
            int cpuActions = 0;
            for (int step = 0; step < 1000 && session.State != MatchSessionState.Finished; step++)
            {
                if (session.State == MatchSessionState.AwaitingHuman)
                {
                    string actionId = session.Snapshot.Actions[0].Id;
                    int turns = session.Game.TurnCount;
                    Assert.That(session.TryApplyHumanAction("not_current"), Is.False);
                    Assert.That(session.Game.TurnCount, Is.EqualTo(turns));
                    Assert.That(session.TryApplyHumanAction(actionId), Is.True);
                    Assert.That(session.TryApplyHumanAction(actionId), Is.False);
                    humanActions++;
                }
                else if (session.State == MatchSessionState.WaitingForCpu)
                {
                    Assert.That(session.TryApplyCpuAction(), Is.True);
                    cpuActions++;
                }
                else
                {
                    Assert.Fail("Unexpected session state: " + session.State);
                }
            }

            Assert.That(session.State, Is.EqualTo(MatchSessionState.Finished));
            Assert.That(session.Game.IsTerminal, Is.True);
            Assert.That(humanActions, Is.GreaterThan(0));
            Assert.That(cpuActions, Is.GreaterThan(0));
            Assert.That(session.Snapshot.Result, Is.Not.Null);
        }

        [Test]
        public void SameRequestReproducesInitialStructuredSnapshot()
        {
            var first = new GameSessionController(seed: 23, wildRank: 7);
            var rematch = new GameSessionController(seed: 23, wildRank: 7);
            first.Begin();
            rematch.Begin();

            Assert.That(SnapshotSignature(rematch.Snapshot),
                Is.EqualTo(SnapshotSignature(first.Snapshot)));
        }

        [Test]
        public void RecordedProductSessionResumesWithTheSameVisibleState()
        {
            var session = new GameSessionController(
                seed: 31, wildRank: 8, difficulty: CpuDifficulties.Hard);
            session.Begin();
            Assert.That(session.Archive.Configuration.Difficulty,
                Is.EqualTo(CpuDifficulties.Hard));
            for (int step = 0; step < 8 && !session.Game.IsTerminal; step++)
            {
                if (session.State == MatchSessionState.AwaitingHuman)
                    Assert.That(session.TryApplyHumanAction(session.Snapshot.Actions[0].Id), Is.True);
                else if (session.State == MatchSessionState.WaitingForCpu)
                    Assert.That(session.TryApplyCpuAction(), Is.True);
            }

            byte[] encoded = SessionArchiveCodec.Encode(session.Archive);
            var resumed = new GameSessionController(SessionArchiveCodec.Decode(encoded));
            resumed.Begin();

            Assert.That(resumed.Archive.Configuration.Difficulty,
                Is.EqualTo(CpuDifficulties.Hard));
            Assert.That(SnapshotSignature(resumed.Snapshot),
                Is.EqualTo(SnapshotSignature(session.Snapshot)));
            Assert.That(resumed.Archive.Actions.Count, Is.EqualTo(session.Archive.Actions.Count));
        }

        [Test]
        public void SessionSlotIdsAreGeneratedCanonicallyAndRejectPaths()
        {
            string id = SessionSlotIds.Create();
            Assert.That(SessionSlotIds.Require(id), Is.EqualTo(id));
            Assert.That(id, Does.Match("^[0-9a-f]{32}$"));
            Assert.Throws<ArgumentException>(() => SessionSlotIds.Require("../save"));
            Assert.Throws<ArgumentException>(() => SessionSlotIds.Require(id.ToUpperInvariant()));
        }

        [Test]
        public void FileSessionStoreSavesUpdatesLoadsAndExplicitlyDeletesSlot()
        {
            string temporaryRoot = Path.Combine(Path.GetTempPath(),
                "TrumpLab-T04-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                var store = new FileSessionStore(temporaryRoot);
                string slotId = SessionSlotIds.Create();
                var session = new GameSessionController(seed: 47, wildRank: 8);
                session.Begin();

                store.Save(slotId, session.Archive);
                Assert.That(store.List().Select(slot => slot.Id), Is.EqualTo(new[] { slotId }));
                Assert.That(store.Load(slotId).Actions.Count, Is.Zero);

                if (session.State == MatchSessionState.AwaitingHuman)
                    Assert.That(session.TryApplyHumanAction(session.Snapshot.Actions[0].Id), Is.True);
                else
                    Assert.That(session.TryApplyCpuAction(), Is.True);
                store.Save(slotId, session.Archive);
                Assert.That(store.Load(slotId).Actions.Count,
                    Is.EqualTo(session.Archive.Actions.Count));

                store.Delete(slotId);
                Assert.That(store.List(), Is.Empty);
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                    Directory.Delete(temporaryRoot, recursive: true);
            }
        }

        [Test]
        public void ResultPresenterReportsHumanOutcomeAndScores()
        {
            var result = new GameResultPresentation(
                winners: new[] { 0 }, scores: new[] { 18d, -18d },
                reason: "empty hand", turns: 31);
            ResultViewModel model = CrazyEightsResultPresenter.Create(result);

            Assert.That(model.Summary, Does.Contain("You win!"));
            Assert.That(model.Summary, Does.Contain("You: 18"));
            Assert.That(model.Summary, Does.Contain("CPU: -18"));
            Assert.That(model.Summary, Does.Contain("empty hand"));
            Assert.That(model.Summary, Does.Contain("31"));
        }

        [Test]
        public void TutorialDefinitionUsesTheCanonicalNormalGameTrace()
        {
            TutorialDefinition definition = TutorialDefinition.CrazyEightsBasic;

            Assert.That(definition.Id, Is.EqualTo("crazy_eights_basic_v1"));
            Assert.That(definition.Version, Is.EqualTo(1));
            Assert.That(definition.Seed, Is.EqualTo(29));
            Assert.That(definition.WildRank, Is.EqualTo(8));
            Assert.That(definition.Difficulty, Is.EqualTo(CpuDifficulties.Standard));
            Assert.That(definition.Trace.Select(TutorialTraceSignature), Is.EqualTo(new[]
            {
                "0|play|3H|-|MatchingPlay",
                "1|play|8H|S|-",
                "0|draw|-|-|Draw",
                "1|play|2S|-|-",
                "0|play|8C|C|WildSuit",
                "1|play|KC|-|-",
                "0|play|5C|-|GuidedPlay",
                "1|play|5S|-|-",
                "0|play|JS|-|GuidedPlay",
                "1|play|7S|-|-",
                "0|play|9S|-|GuidedPlay",
                "1|play_last_card|KS|-|-",
                "0|play|8S|H|GuidedPlay",
                "1|draw|-|-|-",
                "0|play_last_card|4H|-|GuidedPlay",
                "1|draw|-|-|-",
                "0|play|2H|-|Win"
            }));
        }

        [Test]
        public void TutorialRejectsUnexpectedAndStaleActionsThenCompletes()
        {
            var tutorial = new TutorialSessionController();
            tutorial.Begin();
            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.AwaitingIntro));
            Assert.That(tutorial.Lesson, Is.EqualTo(TutorialLesson.Intro));
            Assert.That(tutorial.AcknowledgeIntro(), Is.True, tutorial.FaultMessage);

            bool rejectedUnexpected = false;
            bool rejectedStale = false;
            for (int step = 0; step < 100 &&
                tutorial.State != TutorialSessionState.AwaitingResultConfirmation; step++)
            {
                if (tutorial.State == TutorialSessionState.AwaitingHuman)
                {
                    int turns = tutorial.Game.TurnCount;
                    int recorded = tutorial.Archive.Actions.Count;
                    string expectedActionId = tutorial.ExpectedActionId!;
                    ActionPresentation? unexpected = tutorial.Snapshot.Actions.FirstOrDefault(
                        action => action.Id != expectedActionId);
                    if (!rejectedUnexpected && unexpected != null)
                    {
                        Assert.That(tutorial.TryApplyHumanAction(unexpected.Id), Is.False);
                        Assert.That(tutorial.FeedbackKey,
                            Does.StartWith("tutorial.feedback.expected_"));
                        Assert.That(tutorial.Game.TurnCount, Is.EqualTo(turns));
                        Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(recorded));
                        rejectedUnexpected = true;
                    }
                    if (!rejectedStale)
                    {
                        Assert.That(tutorial.TryApplyHumanAction("stale_action"), Is.False);
                        Assert.That(tutorial.FeedbackKey,
                            Is.EqualTo("tutorial.feedback.stale_action"));
                        Assert.That(tutorial.Game.TurnCount, Is.EqualTo(turns));
                        Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(recorded));
                        rejectedStale = true;
                    }

                    Assert.That(tutorial.TryApplyHumanAction(expectedActionId), Is.True,
                        tutorial.FaultMessage);
                    Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(recorded + 1));
                    Assert.That(tutorial.TryApplyHumanAction(expectedActionId), Is.False);
                    Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(recorded + 1));
                }
                else if (tutorial.State == TutorialSessionState.WaitingForCpu)
                {
                    Assert.That(tutorial.TryApplyCpuAction(), Is.True, tutorial.FaultMessage);
                }
                else
                {
                    Assert.Fail("Unexpected tutorial state: " + tutorial.State + " " +
                        tutorial.FaultMessage);
                }
            }

            Assert.That(rejectedUnexpected, Is.True);
            Assert.That(rejectedStale, Is.True);
            Assert.That(tutorial.State,
                Is.EqualTo(TutorialSessionState.AwaitingResultConfirmation),
                tutorial.FaultMessage);
            Assert.That(tutorial.AppliedActions,
                Is.EqualTo(tutorial.Definition.Trace.Count));
            Assert.That(tutorial.Game.IsTerminal, Is.True);
            Assert.That(tutorial.Game.Result().Winners, Is.EqualTo(new[] { 0 }));
            Assert.That(tutorial.Game.Result().Reason, Is.EqualTo("empty hand"));
            Assert.That(tutorial.Lesson, Is.EqualTo(TutorialLesson.Win));
            Assert.That(tutorial.Archive.Configuration.Seed, Is.EqualTo(29));
            Assert.That(tutorial.Archive.Configuration.Difficulty,
                Is.EqualTo(CpuDifficulties.Standard));
            Assert.That(tutorial.ConfirmResult(), Is.True);
            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.Finished));
        }

        [Test]
        public void HowToPlayUsesStableOrderedPagesAndExplainsResultDetails()
        {
            HowToPlayViewModel rules = CrazyEightsHowToPlayPresenter.Create();

            Assert.That(rules.Pages.Select(page => page.Id), Is.EqualTo(new[]
            {
                HowToPlayPageId.Objective,
                HowToPlayPageId.LegalPlay,
                HowToPlayPageId.Draw,
                HowToPlayPageId.WildSuit,
                HowToPlayPageId.Result
            }));
            Assert.That(rules.Pages.Select(page => page.TextKey), Is.EqualTo(new[]
            {
                "rules.crazy_eights.objective",
                "rules.crazy_eights.legal_play",
                "rules.crazy_eights.draw",
                "rules.crazy_eights.wild_suit",
                "rules.crazy_eights.result"
            }));
            Assert.That(rules.InitialPageIndex, Is.Zero);
            Assert.That(rules.Pages[(int)HowToPlayPageId.Draw].Body,
                Does.Contain("ends your turn"));
            Assert.That(rules.Pages[(int)HowToPlayPageId.WildSuit].Body,
                Does.Contain("called suit"));

            var result = new GameResultPresentation(
                winners: new[] { 0 }, scores: new[] { 18d, -18d },
                reason: "empty hand", turns: 31);
            HowToPlayViewModel resultGuide = CrazyEightsHowToPlayPresenter.Create(result: result);
            HowToPlayPage resultPage = resultGuide.Pages[resultGuide.InitialPageIndex];

            Assert.That(resultPage.Id, Is.EqualTo(HowToPlayPageId.Result));
            Assert.That(resultPage.Body, Does.Contain("Current result"));
            Assert.That(resultPage.Body, Does.Contain("You: 18"));
            Assert.That(resultPage.Body, Does.Contain("CPU: -18"));
            Assert.That(resultPage.Body, Does.Contain("a player emptied their hand"));
            Assert.That(resultPage.Body, Does.Contain("Turns: 31"));
        }

        [Test]
        public void ProductProgressRoundTripsAndRefusesToOverwriteCorruption()
        {
            string temporaryRoot = Path.Combine(Path.GetTempPath(),
                "TrumpLab-M05-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                var store = new FileProductProgressStore(temporaryRoot);
                TutorialDefinition definition = TutorialDefinition.CrazyEightsBasic;
                Assert.That(store.Load().IsTutorialCompleted(definition), Is.False);

                store.SaveTutorialCompleted(definition);
                ProductProgress loaded = store.Load();
                Assert.That(loaded.FormatVersion,
                    Is.EqualTo(ProductProgress.CurrentFormatVersion));
                Assert.That(loaded.TutorialId, Is.EqualTo(definition.Id));
                Assert.That(loaded.TutorialVersion, Is.EqualTo(definition.Version));
                Assert.That(loaded.IsTutorialCompleted(definition), Is.True);
                byte[] completed = File.ReadAllBytes(store.ProgressPath);
                store.SaveTutorialCompleted(definition);
                Assert.That(File.ReadAllBytes(store.ProgressPath), Is.EqualTo(completed));

                File.WriteAllText(store.ProgressPath, "corrupt progress");
                byte[] corrupt = File.ReadAllBytes(store.ProgressPath);
                Assert.Throws<ProductProgressFormatException>(() => store.Load());
                Assert.Throws<ProductProgressFormatException>(() =>
                    store.SaveTutorialCompleted(definition));
                Assert.That(File.ReadAllBytes(store.ProgressPath), Is.EqualTo(corrupt));
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                    Directory.Delete(temporaryRoot, recursive: true);
            }
        }

        [Test]
        public void LocalizationCatalogHasExactKeysAndLocalePlaceholderParity()
        {
            Assert.DoesNotThrow(ProductTextCatalog.Validate);
            Assert.That(ProductTextCatalog.TryValidate(out string validationError), Is.True,
                validationError);

            string[] keys = ProductTextCatalog.Keys.ToArray();
            Assert.That(keys,
                Is.EqualTo(keys.OrderBy(key => key, StringComparer.Ordinal).ToArray()));
            Assert.That(keys, Is.Unique);
            Assert.That(keys, Has.Length.EqualTo(254));
            using SHA256 sha256 = SHA256.Create();
            string signature = BitConverter.ToString(sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(string.Join("\n", keys))))
                .Replace("-", string.Empty).ToLowerInvariant();
            Assert.That(signature,
                Is.EqualTo("2491e1e89747a3c49e972f0a924c65f03fbdcbb4dbba05027830c1e2dbc1c61a"),
                "Intentional catalog changes must update the exact key-set contract.");

            Assert.That(ProductTextCatalog.All.Select(entry => entry.Key),
                Is.EquivalentTo(keys));
            foreach (ProductTextEntry entry in ProductTextCatalog.All)
            {
                Assert.That(ProductTextCatalog.Entry(entry.Key), Is.SameAs(entry));
                Assert.That(entry.PlaceholderIndexes, Is.Ordered);
                object[] arguments = Enumerable.Range(0, entry.ArgumentCount)
                    .Select(index => (object)("ARG" + index)).ToArray();
                foreach (string locale in new[]
                    { ProductTextCatalog.EnglishLocale, ProductTextCatalog.JapaneseLocale })
                {
                    string localized = ProductTextCatalog.ForLocale(locale)
                        .Get(entry.Key, arguments);
                    Assert.That(localized, Is.Not.Null.And.Not.Empty,
                        locale + ":" + entry.Key);
                    Assert.That(localized, Is.Not.EqualTo(entry.Key),
                        locale + " exposed a raw key: " + entry.Key);
                }
            }
        }

        [Test]
        public void GeneratedUiHasCompleteLocalizationAndAccessibilityContracts()
        {
            WithBootstrapScene(roots =>
            {
                Canvas canvas = roots.Select(root => root.GetComponent<Canvas>())
                    .Single(component => component != null);
                RectTransform canvasRect = (RectTransform)canvas.transform;
                ProductSafeFrame safeFrame = canvas.GetComponentInChildren<ProductSafeFrame>(true);
                Assert.That(safeFrame, Is.Not.Null);
                Assert.That(safeFrame.transform.parent, Is.SameAs(canvas.transform));
                Assert.That(safeFrame.ParentRect, Is.SameAs(canvasRect));

                GameObject productRoot = roots.Single(root => root.name == "ProductRoot");
                ProductAppController app = productRoot.GetComponent<ProductAppController>();
                ProductLocalizationController localization =
                    productRoot.GetComponent<ProductLocalizationController>();
                ProductAccessibilityController accessibility =
                    productRoot.GetComponent<ProductAccessibilityController>();
                Assert.That(app, Is.Not.Null);
                Assert.That(localization, Is.Not.Null);
                Assert.That(accessibility, Is.Not.Null);
                Assert.That(app.LocalizationController, Is.SameAs(localization));
                Assert.That(app.AccessibilityController, Is.SameAs(accessibility));
                Assert.That(localization.UiRoot, Is.SameAs(canvas.transform));
                Assert.That(accessibility.UiRoot, Is.SameAs(canvasRect));
                Assert.That(accessibility.SafeFrame, Is.SameAs(safeFrame));
                Assert.That(accessibility.Text, Is.SameAs(localization));

                ProductTextElement[] textElements =
                    canvas.GetComponentsInChildren<ProductTextElement>(true);
                Text[] texts = canvas.GetComponentsInChildren<Text>(true);
                Assert.That(textElements, Has.Length.EqualTo(texts.Length));
                foreach (Text text in texts)
                {
                    ProductTextElement? element = text.GetComponent<ProductTextElement>();
                    Assert.That(element, Is.Not.Null, PathOf(text.transform));
                    Assert.That(element!.Target, Is.SameAs(text), PathOf(text.transform));
                    Assert.That(element.BaseFontSize, Is.GreaterThan(0), PathOf(text.transform));
                    Assert.That(text.fontSize, Is.EqualTo(element.BaseFontSize),
                        PathOf(text.transform) + " must start from its immutable 100% size");
                    Assert.That(text.resizeTextForBestFit, Is.False, PathOf(text.transform));
                    Assert.DoesNotThrow(() => ProductTextCatalog.RequireStableKey(
                        element.StableKey, nameof(element.StableKey)), PathOf(text.transform));
                    if (element.ContentMode == ProductTextContentMode.Static)
                    {
                        Assert.That(ProductTextCatalog.Contains(element.StableKey), Is.True,
                            PathOf(text.transform));
                        Assert.That(text.text,
                            Is.EqualTo(ProductTextCatalog.English.Get(element.StableKey)),
                            PathOf(text.transform));
                    }
                }

                Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
                Assert.That(graphics, Is.Not.Empty);
                foreach (Graphic graphic in graphics)
                {
                    ProductGraphicElement? element =
                        graphic.GetComponent<ProductGraphicElement>();
                    Assert.That(element, Is.Not.Null, PathOf(graphic.transform));
                    Assert.That(element!.TargetGraphic, Is.SameAs(graphic),
                        PathOf(graphic.transform));
                    Assert.That(Enum.IsDefined(typeof(ProductGraphicRole), element.BaseRole),
                        Is.True, PathOf(graphic.transform));
                }

                Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(true);
                Assert.That(selectables, Is.Not.Empty);
                foreach (Selectable selectable in selectables)
                {
                    ProductAccessibleControl? accessible =
                        selectable.GetComponent<ProductAccessibleControl>();
                    Assert.That(accessible, Is.Not.Null, PathOf(selectable.transform));
                    Assert.That(accessible!.Control, Is.SameAs(selectable),
                        PathOf(selectable.transform));
                    Assert.That(ProductTextCatalog.Contains(accessible.LabelKey), Is.True,
                        PathOf(selectable.transform) + " label " + accessible.LabelKey);
                    Assert.That(ProductTextCatalog.Entry(accessible.LabelKey).ArgumentCount,
                        Is.Zero, PathOf(selectable.transform) +
                        " accessible labels cannot require formatting arguments");
                    Assert.That(accessible.HasMinimumReferenceHitTarget, Is.True,
                        PathOf(selectable.transform) + " is " + accessible.ReferenceHitSize);
                    Assert.That(accessible.FocusOutline, Is.Not.Null,
                        PathOf(selectable.transform));
                }

                foreach (ProductScreen screen in
                    canvas.GetComponentsInChildren<ProductScreen>(true))
                    Assert.That(screen.transform.IsChildOf(safeFrame.transform), Is.True,
                        PathOf(screen.transform));
                ProductErrorPanel error = canvas.GetComponentInChildren<ProductErrorPanel>(true);
                ProductPresentationController presentation =
                    canvas.GetComponent<ProductPresentationController>();
                Assert.That(error.transform.IsChildOf(safeFrame.transform), Is.True);
                Assert.That(presentation.Banner.transform.IsChildOf(safeFrame.transform), Is.True);
                Assert.That(presentation.Transition.transform.IsChildOf(safeFrame.transform),
                    Is.True);
            });
        }

        [Test]
        public void ProductDoesNotBundleFontAssets()
        {
            const string productAssetRoot = "Assets/TrumpLab/Product";
            Assert.That(AssetDatabase.FindAssets("t:Font", new[] { productAssetRoot }), Is.Empty);
            string productDirectory = Path.Combine(Application.dataPath, "TrumpLab", "Product");
            string[] fontFiles = Directory.GetFiles(productDirectory, "*",
                    SearchOption.AllDirectories)
                .Where(path => new[] { ".ttf", ".ttc", ".otf", ".woff", ".woff2" }
                    .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .ToArray();
            Assert.That(fontFiles, Is.Empty);
        }

        [Test]
        public void GeneratedUiFitsAllLocaleScaleAndResolutionCombinations()
        {
            Vector2Int[] resolutions =
            {
                new Vector2Int(1280, 720), new Vector2Int(1280, 800),
                new Vector2Int(1920, 1080), new Vector2Int(1920, 1200),
                new Vector2Int(2560, 1080), new Vector2Int(3440, 1440),
                new Vector2Int(3840, 2160)
            };
            string[] locales =
                { ProductTextCatalog.EnglishLocale, ProductTextCatalog.JapaneseLocale };

            WithBootstrapScene(roots =>
            {
                Canvas canvas = roots.Select(root => root.GetComponent<Canvas>())
                    .Single(component => component != null);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode,
                    Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.screenMatchMode,
                    Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
                RectTransform canvasRect = (RectTransform)canvas.transform;
                ProductSafeFrame safeFrame = canvas.GetComponentInChildren<ProductSafeFrame>(true);
                GameObject productRoot = roots.Single(root => root.name == "ProductRoot");
                ProductLocalizationController localization =
                    productRoot.GetComponent<ProductLocalizationController>();
                ProductAccessibilityController accessibility =
                    productRoot.GetComponent<ProductAccessibilityController>();
                localization.SetFontHost(new DeterministicProductFontHost(
                    localization.FallbackFont));
                canvas.renderMode = RenderMode.WorldSpace;

                foreach (string locale in locales)
                foreach (int scale in ProductSettings.SupportedTextScalePercents)
                foreach (Vector2Int resolution in resolutions)
                {
                    string matrix = locale + " / " + scale + "% / " +
                        resolution.x + "x" + resolution.y;
                    canvasRect.sizeDelta = LogicalCanvasSize(scaler, resolution);
                    safeFrame.ApplyFrame();
                    ProductSettings settings = ProductSettings.CreateDefaults(locale)
                        .WithTextScalePercent(scale);
                    localization.Apply(settings);
                    RenderRepresentativeLocalizedStates(canvas, localization, settings);
                    accessibility.Apply(settings);
                    RebuildLayouts(canvas.transform);

                    Assert.That(safeFrame.Frame.rect.width, Is.GreaterThan(0f), matrix);
                    Assert.That(safeFrame.Frame.rect.height, Is.GreaterThan(0f), matrix);
                    Assert.That(safeFrame.Frame.rect.width / safeFrame.Frame.rect.height,
                        Is.EqualTo(ProductSafeFrame.TargetAspectRatio).Within(0.001f), matrix);

                    var textLayoutIssues = new List<string>();
                    foreach (ProductTextElement element in
                        canvas.GetComponentsInChildren<ProductTextElement>(true))
                    {
                        Text text = element.Target;
                        RectTransform rect = text.rectTransform;
                        if (!IsInsideClippedScrollContent(rect))
                            AssertRectInsideSafeFrame(rect, safeFrame.Frame, matrix);
                        Assert.That(rect.rect.width, Is.GreaterThan(0.01f),
                            matrix + " / " + PathOf(rect));
                        Assert.That(rect.rect.height, Is.GreaterThan(0.01f),
                            matrix + " / " + PathOf(rect));
                        if (string.IsNullOrEmpty(text.text)) continue;
                        Assert.That(text.preferredWidth, Is.GreaterThan(0f),
                            matrix + " / " + PathOf(rect));
                        Assert.That(text.preferredHeight, Is.GreaterThan(0f),
                            matrix + " / " + PathOf(rect));
                        if (text.preferredHeight > rect.rect.height + 1f)
                            textLayoutIssues.Add(matrix + " / vertical text overflow at " +
                                PathOf(rect) + " (preferred " + text.preferredHeight +
                                ", available " + rect.rect.height + ")");
                        if (text.horizontalOverflow == HorizontalWrapMode.Overflow)
                        {
                            if (text.preferredWidth > rect.rect.width + 1f)
                                textLayoutIssues.Add(matrix +
                                    " / horizontal text overflow at " + PathOf(rect) +
                                    " (preferred " + text.preferredWidth +
                                    ", available " + rect.rect.width + ")");
                        }
                    }
                    Assert.That(textLayoutIssues, Is.Empty,
                        "Localized text does not fit:\n" +
                        string.Join("\n", textLayoutIssues));

                    foreach (Selectable selectable in
                        canvas.GetComponentsInChildren<Selectable>(true))
                    {
                        RectTransform rect = (RectTransform)selectable.transform;
                        if (!IsInsideClippedScrollContent(rect))
                            AssertRectInsideSafeFrame(rect, safeFrame.Frame, matrix);
                        Assert.That(rect.rect.width, Is.GreaterThan(0.01f),
                            matrix + " / " + PathOf(rect));
                        Assert.That(rect.rect.height, Is.GreaterThan(0.01f),
                            matrix + " / " + PathOf(rect));
                    }

                    AssertIndependentLeafElementsDoNotOverlap(canvas, matrix);
                    AssertMatchDynamicControlsRespectActionViewport(canvas, matrix);
                }
            });
        }

        [Test]
        public void ProductPrefabsAndBootstrapSceneHaveNoMissingScripts()
        {
            string[] prefabs =
            {
                "TitleScreen.prefab",
                "GameSettingsScreen.prefab",
                "ProductSettingsScreen.prefab",
                "SessionLibraryScreen.prefab",
                "MatchScreen.prefab",
                "ReplayScreen.prefab",
                "ResultScreen.prefab",
                "HowToPlayScreen.prefab"
            };
            foreach (string fileName in prefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/TrumpLab/Product/Prefabs/Screens/" + fileName);
                Assert.That(prefab, Is.Not.Null, fileName);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab),
                    Is.Zero, fileName);
            }

            const string scenePath = "Assets/TrumpLab/Product/Scenes/Bootstrap.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                GameObject[] roots = scene.GetRootGameObjects();
                InputSystemUIInputModule[] inputModules = roots.SelectMany(root =>
                    root.GetComponentsInChildren<InputSystemUIInputModule>(true)).ToArray();
                StandaloneInputModule[] legacyInputModules = roots.SelectMany(root =>
                    root.GetComponentsInChildren<StandaloneInputModule>(true)).ToArray();

                Assert.That(inputModules, Has.Length.EqualTo(1));
                Assert.That(legacyInputModules, Is.Empty);

                GameObject? productRoot = roots.SingleOrDefault(root =>
                    root.name == "ProductRoot");
                Assert.That(productRoot, Is.Not.Null);
                Assert.That(productRoot!.GetComponent<ProductInputController>(), Is.Not.Null);

                AudioListener[] listeners = roots.SelectMany(root =>
                    root.GetComponentsInChildren<AudioListener>(true)).ToArray();
                AudioSource[] audioSources = roots.SelectMany(root =>
                    root.GetComponentsInChildren<AudioSource>(true)).ToArray();
                Assert.That(listeners, Has.Length.EqualTo(1));
                Assert.That(listeners[0].gameObject, Is.SameAs(productRoot));
                Assert.That(audioSources, Has.Length.EqualTo(2));
                ProductAudioController? audio =
                    productRoot.GetComponent<ProductAudioController>();
                Assert.That(audio, Is.Not.Null);
                audio!.Initialize();
                Assert.That(audio.IsInitialized, Is.True);
                Assert.That(audio.MusicSource, Is.Not.SameAs(audio.SfxSource));
                Assert.That(audio.MusicSource.gameObject.name, Is.EqualTo("MusicAudio"));
                Assert.That(audio.SfxSource.gameObject.name, Is.EqualTo("SfxAudio"));
                Assert.That(audio.MusicSource.transform.parent, Is.SameAs(productRoot.transform));
                Assert.That(audio.SfxSource.transform.parent, Is.SameAs(productRoot.transform));
                Assert.That(audio.MusicSource.spatialBlend, Is.Zero);
                Assert.That(audio.SfxSource.spatialBlend, Is.Zero);
                Assert.That(audio.MusicSource.loop, Is.True);
                Assert.That(audio.SfxSource.loop, Is.False);
                Assert.That(audio.MusicSource.clip, Is.SameAs(audio.MusicLoop));
                foreach (ProductFeedbackKind kind in Enum.GetValues(typeof(ProductFeedbackKind)))
                    Assert.DoesNotThrow(() => audio.Play(kind), kind.ToString());

                Canvas? canvas = roots.Select(root => root.GetComponent<Canvas>())
                    .SingleOrDefault(component => component != null);
                Assert.That(canvas, Is.Not.Null);
                ProductPresentationController? presentation =
                    canvas!.GetComponent<ProductPresentationController>();
                Assert.That(presentation, Is.Not.Null);
                ProductSafeFrame generatedSafeFrame =
                    canvas.GetComponentInChildren<ProductSafeFrame>(true);
                Assert.That(generatedSafeFrame, Is.Not.Null);
                Assert.That(presentation!.Banner.transform.parent,
                    Is.SameAs(generatedSafeFrame.transform));
                Assert.That(presentation.Transition.transform.parent,
                    Is.SameAs(generatedSafeFrame.transform));
                Assert.That(presentation.Banner.blocksRaycasts, Is.False);
                Assert.That(presentation.Transition.blocksRaycasts, Is.False);
                Assert.That(presentation.Banner.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
                    .Concat(presentation.Transition
                        .GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    .All(graphic => !graphic.raycastTarget), Is.True);

                UnityEngine.UI.Selectable[] selectables =
                    canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>(true);
                ProductUiFeedbackEmitter[] emitters =
                    canvas.GetComponentsInChildren<ProductUiFeedbackEmitter>(true);
                Assert.That(selectables, Is.Not.Empty);
                Assert.That(emitters, Has.Length.EqualTo(selectables.Length));
                Assert.That(selectables.All(selectable =>
                    selectable.GetComponent<ProductUiFeedbackEmitter>() != null), Is.True);
                foreach (UnityEngine.UI.Selectable control in selectables)
                {
                    Component[] components = control.GetComponents<Component>();
                    int controlIndex = Array.IndexOf(components, control);
                    int emitterIndex = Array.FindIndex(components,
                        component => component is ProductUiFeedbackEmitter);
                    Assert.That(emitterIndex, Is.GreaterThanOrEqualTo(0), control.name);
                    Assert.That(emitterIndex, Is.LessThan(controlIndex),
                        control.name + " must emit Submit before its action callback.");
                }
                Assert.That(emitters.All(emitter =>
                    emitter.GetComponent<UnityEngine.UI.Selectable>() != null &&
                    emitter.GetComponentsInParent<ProductPresentationController>(true)
                        .SingleOrDefault() == presentation), Is.True);

                ScreenRouter? router = productRoot!.GetComponent<ScreenRouter>();
                Assert.That(router, Is.Not.Null);
                ScreenId[] expectedIds = Enum.GetValues(typeof(ScreenId))
                    .Cast<ScreenId>().ToArray();
                Assert.That(router!.Screens.Count, Is.EqualTo(expectedIds.Length));
                Assert.That(router.Screens.Select(screen => screen.Id),
                    Is.EquivalentTo(expectedIds));
                ProductAppController? app = productRoot.GetComponent<ProductAppController>();
                Assert.That(app, Is.Not.Null);
                Assert.That(app!.PresentationController, Is.SameAs(presentation));

                var library = (SessionLibraryScreen)router.Get(ScreenId.SessionLibrary);
                library.SetSlots(Array.Empty<SessionSlotInfo>());
                router.Show(ScreenId.SessionLibrary);
                UnityEngine.UI.Selectable? selectable =
                    ScreenRouter.FindFocusTarget(library);
                Assert.That(selectable, Is.Not.Null);
                Assert.That(selectable!.gameObject.activeInHierarchy, Is.True);
                Assert.That(selectable!.IsActive() && selectable.IsInteractable(), Is.True);

                var match = (MatchScreen)router.Get(ScreenId.Match);
                Assert.That(match.ActionButtonTemplate
                    .GetComponent<ProductUiFeedbackEmitter>().SubmitFeedbackEnabled, Is.False,
                    "Match actions use record-derived semantic feedback, not generic Submit.");
            }
            finally
            {
                if (previousSetup.Any(setup => setup.isLoaded))
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void GeneratedAudioAssetsUseOwnedPcmContract()
        {
            const string audioDirectory = "Assets/TrumpLab/Product/Audio/Generated";
            string[] expectedFiles =
            {
                "card-play.wav", "cpu-turn.wav", "draw.wav", "error.wav", "lose.wav",
                "music-loop.wav", "navigation.wav", "reject.wav", "submit.wav",
                "wild-suit.wav", "win.wav"
            };
            string[] paths = AssetDatabase.FindAssets("t:AudioClip", new[] { audioDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(paths.Select(Path.GetFileName), Is.EqualTo(expectedFiles));
            var waveformSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                AudioClip? clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Assert.That(clip, Is.Not.Null, path);
                Assert.That(clip!.channels, Is.EqualTo(1), path);
                Assert.That(clip.frequency, Is.EqualTo(44100), path);
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer!.forceToMono, Is.True, path);
                Assert.That(importer.loadInBackground, Is.False, path);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                Assert.That(settings.preloadAudioData, Is.True, path);
                Assert.That(settings.loadType,
                    Is.EqualTo(AudioClipLoadType.DecompressOnLoad), path);
                Assert.That(settings.compressionFormat,
                    Is.EqualTo(AudioCompressionFormat.PCM), path);
                Assert.That(settings.sampleRateSetting,
                    Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate), path);

                byte[] wave = File.ReadAllBytes(path);
                Assert.That(wave.Length, Is.GreaterThan(44), path);
                Assert.That(wave.Skip(44).Any(value => value != 0), Is.True,
                    path + " must contain a non-silent PCM payload.");
                using SHA256 sha256 = SHA256.Create();
                string signature = Convert.ToBase64String(sha256.ComputeHash(wave));
                Assert.That(waveformSignatures.Add(signature), Is.True,
                    path + " must have a distinct generated waveform.");
            }
        }

        private static void WithBootstrapScene(Action<GameObject[]> assertion)
        {
            const string scenePath = "Assets/TrumpLab/Product/Scenes/Bootstrap.unity";
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                assertion(scene.GetRootGameObjects());
            }
            finally
            {
                if (previousSetup.Any(setup => setup.isLoaded))
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void RenderRepresentativeLocalizedStates(Canvas canvas,
            IProductText text, ProductSettings settings)
        {
            TitleScreen title = canvas.GetComponentInChildren<TitleScreen>(true);
            title.SetText(text);
            title.SetTutorialCompleted(completed: true);

            GameSettingsScreen gameSettings =
                canvas.GetComponentInChildren<GameSettingsScreen>(true);
            gameSettings.SetText(text);
            gameSettings.SetValues(new GameStartRequest(
                -9223372036854775807L, wildRank: 13, difficulty: CpuDifficulties.Hard));

            ProductSettingsScreen productSettings =
                canvas.GetComponentInChildren<ProductSettingsScreen>(true);
            productSettings.SetText(text);
            productSettings.SetValues(settings, text.Get("settings.feedback_applied"));

            SessionLibraryScreen library =
                canvas.GetComponentInChildren<SessionLibraryScreen>(true);
            library.SetText(text);
            library.SetSlots(new[]
            {
                new SessionSlotInfo("0123456789abcdef0123456789abcdef",
                    new DateTime(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc))
            });

            var session = new GameSessionController(seed: 29, wildRank: 8,
                difficulty: CpuDifficulties.Hard);
            session.Begin();
            ReplayScreen replay = canvas.GetComponentInChildren<ReplayScreen>(true);
            replay.SetText(text);
            replay.Render(session.Snapshot, appliedActions: 999);

            var result = new GameResultPresentation(
                winners: new[] { 0 }, scores: new[] { 9999d, -9999d },
                reason: "empty hand", turns: 9999);
            ResultScreen resultScreen = canvas.GetComponentInChildren<ResultScreen>(true);
            resultScreen.SetText(text);
            resultScreen.Render(CrazyEightsResultPresenter.Create(result, text: text));

            HowToPlayScreen rules = canvas.GetComponentInChildren<HowToPlayScreen>(true);
            rules.SetText(text);
            HowToPlayViewModel guide = CrazyEightsHowToPlayPresenter.Create(
                session.Snapshot, result, text);
            int longestPage = guide.Pages.Select((page, index) => new
                { Length = page.Body.Length, Index = index })
                .OrderByDescending(candidate => candidate.Length)
                .First().Index;
            rules.Render(new HowToPlayViewModel(
                guide.Pages, longestPage, guide.Context));

            var tutorial = new TutorialSessionController();
            tutorial.Begin();
            MatchScreen match = canvas.GetComponentInChildren<MatchScreen>(true);
            match.SetText(text);
            match.RenderTutorial(CrazyEightsMatchPresenter.Create(
                    tutorial.Snapshot, inputEnabled: false, text: text),
                TutorialOverlayPresenter.Create(tutorial, text));
            bool matchWasActive = match.gameObject.activeSelf;
            match.gameObject.SetActive(true);
            RebuildLayouts(match.transform);
            match.gameObject.SetActive(matchWasActive);

            ProductErrorPanel error = canvas.GetComponentInChildren<ProductErrorPanel>(true);
            error.SetText(text);
            error.MessageLabel.text = text.Get("error.gamepad_disconnected");
            error.gameObject.SetActive(true);
            canvas.GetComponent<ProductPresentationController>().SetText(text);
        }

        private static Vector2 LogicalCanvasSize(CanvasScaler scaler, Vector2Int resolution)
        {
            Vector2 reference = scaler.referenceResolution;
            float widthScale = resolution.x / reference.x;
            float heightScale = resolution.y / reference.y;
            float logarithmicWidth = Mathf.Log(widthScale, 2f);
            float logarithmicHeight = Mathf.Log(heightScale, 2f);
            float scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logarithmicWidth,
                logarithmicHeight, scaler.matchWidthOrHeight));
            return new Vector2(resolution.x / scaleFactor, resolution.y / scaleFactor);
        }

        private static void RebuildLayouts(Transform root)
        {
            Canvas.ForceUpdateCanvases();
            foreach (LayoutGroup group in root.GetComponentsInChildren<LayoutGroup>(true))
            {
                if (group.transform is RectTransform rect)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertRectInsideSafeFrame(RectTransform rect,
            RectTransform safeFrame, string matrix)
        {
            Assert.That(rect == safeFrame || rect.IsChildOf(safeFrame), Is.True,
                matrix + " / outside safe-frame hierarchy: " + PathOf(rect));
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Rect safe = safeFrame.rect;
            const float tolerance = 1f;
            foreach (Vector3 corner in corners)
            {
                Vector3 local = safeFrame.InverseTransformPoint(corner);
                Assert.That(local.x, Is.GreaterThanOrEqualTo(safe.xMin - tolerance),
                    matrix + " / left overflow: " + PathOf(rect));
                Assert.That(local.x, Is.LessThanOrEqualTo(safe.xMax + tolerance),
                    matrix + " / right overflow: " + PathOf(rect));
                Assert.That(local.y, Is.GreaterThanOrEqualTo(safe.yMin - tolerance),
                    matrix + " / bottom overflow: " + PathOf(rect));
                Assert.That(local.y, Is.LessThanOrEqualTo(safe.yMax + tolerance),
                    matrix + " / top overflow: " + PathOf(rect));
            }
        }

        private static bool IsInsideClippedScrollContent(RectTransform rect)
        {
            ScrollRect? scroll = rect.GetComponentInParent<ScrollRect>(includeInactive: true);
            if (scroll == null || scroll.content == null || scroll.viewport == null ||
                scroll.viewport.GetComponent<Mask>() == null) return false;
            return rect == scroll.content || rect.IsChildOf(scroll.content);
        }

        private static void AssertIndependentLeafElementsDoNotOverlap(
            Canvas canvas, string matrix)
        {
            var overlaps = new List<string>();
            foreach (ProductScreen screen in
                canvas.GetComponentsInChildren<ProductScreen>(includeInactive: true))
            {
                bool screenWasActive = screen.gameObject.activeSelf;
                screen.gameObject.SetActive(true);
                try
                {
                    if (screen is ProductSettingsScreen)
                    {
                        CollectProductSettingsPageStateOverlaps(screen, matrix, overlaps);
                    }
                    else if (screen is MatchScreen)
                    {
                        CollectMatchSurfaceStateOverlaps(screen, matrix, overlaps);
                    }
                    else
                    {
                        RebuildLayouts(screen.transform);
                        CollectActiveLeafElementOverlaps(
                            screen.transform, (RectTransform)screen.transform,
                            matrix + " / " + screen.Id, overlaps);
                    }
                }
                finally
                {
                    screen.gameObject.SetActive(screenWasActive);
                }
            }

            ProductErrorPanel error = canvas.GetComponentInChildren<ProductErrorPanel>(true);
            bool errorWasActive = error.gameObject.activeSelf;
            error.gameObject.SetActive(true);
            try
            {
                RebuildLayouts(error.transform);
                CollectActiveLeafElementOverlaps(error.transform,
                    (RectTransform)error.transform, matrix + " / ErrorPanel", overlaps);
            }
            finally
            {
                error.gameObject.SetActive(errorWasActive);
            }

            Assert.That(overlaps, Is.Empty,
                "Independent UI elements overlap:\n" + string.Join("\n", overlaps));
        }

        private static void CollectProductSettingsPageStateOverlaps(
            ProductScreen screen, string matrix, ICollection<string> overlaps)
        {
            string[] panelNames = { "GeneralPanel", "BindingsPanel", "AccessibilityPanel" };
            Transform[] panels = panelNames.Select(name =>
                    screen.GetComponentsInChildren<Transform>(includeInactive: true)
                        .Single(candidate => candidate.name == name))
                .ToArray();
            bool[] previousStates = panels.Select(panel => panel.gameObject.activeSelf).ToArray();
            try
            {
                foreach (Transform activePanel in panels)
                {
                    foreach (Transform panel in panels)
                        panel.gameObject.SetActive(panel == activePanel);
                    RebuildLayouts(screen.transform);
                    CollectActiveLeafElementOverlaps(
                        screen.transform, (RectTransform)screen.transform,
                        matrix + " / ProductSettings/" + activePanel.name, overlaps);
                }
            }
            finally
            {
                for (int index = 0; index < panels.Length; index++)
                    panels[index].gameObject.SetActive(previousStates[index]);
            }
        }

        private static void CollectMatchSurfaceStateOverlaps(
            ProductScreen screen, string matrix, ICollection<string> overlaps)
        {
            string[] overlayNames = { "ContextHelpPanel", "TutorialPanel" };
            Transform[] overlays = overlayNames.Select(name =>
                    screen.GetComponentsInChildren<Transform>(includeInactive: true)
                        .Single(candidate => candidate.name == name))
                .ToArray();
            bool[] previousStates = overlays.Select(panel => panel.gameObject.activeSelf).ToArray();
            try
            {
                foreach (Transform overlay in overlays) overlay.gameObject.SetActive(false);
                RebuildLayouts(screen.transform);
                CollectActiveLeafElementOverlaps(screen.transform,
                    (RectTransform)screen.transform, matrix + " / Match/base", overlaps);

                // Modal surfaces intentionally cover the board. Validate their own leaf
                // layout independently without treating the obscured board as a collision.
                foreach (Transform overlay in overlays)
                {
                    overlay.gameObject.SetActive(true);
                    RebuildLayouts(overlay);
                    CollectActiveLeafElementOverlaps(overlay,
                        (RectTransform)overlay, matrix + " / Match/" + overlay.name,
                        overlaps);
                    overlay.gameObject.SetActive(false);
                }
            }
            finally
            {
                for (int index = 0; index < overlays.Length; index++)
                    overlays[index].gameObject.SetActive(previousStates[index]);
            }
        }

        private static void CollectActiveLeafElementOverlaps(Transform scope,
            RectTransform coordinateSpace, string matrix, ICollection<string> overlaps)
        {
            Component[] elements = scope.GetComponentsInChildren<Text>(includeInactive: false)
                .Where(text => !string.IsNullOrWhiteSpace(text.text))
                .Cast<Component>()
                .Concat(scope.GetComponentsInChildren<Selectable>(includeInactive: false)
                    .Where(control => control.interactable)
                    .Cast<Component>())
                .ToArray();
            for (int firstIndex = 0; firstIndex < elements.Length; firstIndex++)
            for (int secondIndex = firstIndex + 1;
                secondIndex < elements.Length; secondIndex++)
            {
                Transform first = elements[firstIndex].transform;
                Transform second = elements[secondIndex].transform;
                if (first == second || first.IsChildOf(second) || second.IsChildOf(first))
                    continue;
                Selectable? firstOwner = first.GetComponentInParent<Selectable>(
                    includeInactive: true);
                Selectable? secondOwner = second.GetComponentInParent<Selectable>(
                    includeInactive: true);
                if (firstOwner != null && firstOwner == secondOwner)
                    continue;

                Rect firstBounds = RectRelativeTo((RectTransform)first, coordinateSpace);
                Rect secondBounds = RectRelativeTo((RectTransform)second, coordinateSpace);
                float horizontalOverlap = Mathf.Min(firstBounds.xMax, secondBounds.xMax) -
                    Mathf.Max(firstBounds.xMin, secondBounds.xMin);
                float verticalOverlap = Mathf.Min(firstBounds.yMax, secondBounds.yMax) -
                    Mathf.Max(firstBounds.yMin, secondBounds.yMin);
                if (horizontalOverlap > 1f && verticalOverlap > 1f)
                    overlaps.Add(matrix + " / independent leaf elements overlap: " +
                        PathOf(first) + " and " + PathOf(second));
            }
        }

        private static void AssertMatchDynamicControlsRespectActionViewport(
            Canvas canvas, string matrix)
        {
            MatchScreen match = canvas.GetComponentInChildren<MatchScreen>(includeInactive: true);
            ScrollRect scroll = match.ActionRoot.GetComponentInParent<ScrollRect>(
                includeInactive: true);
            Assert.That(scroll, Is.Not.Null, matrix + " / Match ActionRoot ScrollRect");
            Assert.That(scroll.viewport, Is.Not.Null, matrix + " / Match ActionViewport");
            Assert.That(scroll.content, Is.SameAs(match.ActionRoot), matrix);
            Assert.That(scroll.horizontal, Is.False, matrix);
            Assert.That(scroll.vertical, Is.True, matrix);
            Assert.That(scroll.viewport.GetComponent<Mask>(), Is.Not.Null,
                matrix + " / ActionViewport must clip vertically scrolled controls.");

            Button[] dynamicControls = match.ActionRoot.GetComponentsInChildren<Button>(
                    includeInactive: true)
                .Where(button => button != match.ActionButtonTemplate &&
                    button.gameObject.activeSelf)
                .ToArray();
            Assert.That(dynamicControls, Is.Not.Empty,
                matrix + " / representative Match actions");
            var resolvedLabels = new List<string>(dynamicControls.Length);
            foreach (Button control in dynamicControls)
            {
                RectTransform rect = (RectTransform)control.transform;
                Assert.That(rect.IsChildOf(scroll.content), Is.True,
                    matrix + " / " + PathOf(rect));
                Rect inContent = RectRelativeTo(rect, scroll.content);
                AssertRectWithin(inContent, scroll.content.rect, matrix +
                    " / action outside scroll content: " + PathOf(rect));

                Rect inViewport = RectRelativeTo(rect, scroll.viewport);
                const float tolerance = 1f;
                Assert.That(inViewport.xMin,
                    Is.GreaterThanOrEqualTo(scroll.viewport.rect.xMin - tolerance),
                    matrix + " / action left of ActionViewport: " + PathOf(rect));
                Assert.That(inViewport.xMax,
                    Is.LessThanOrEqualTo(scroll.viewport.rect.xMax + tolerance),
                    matrix + " / action right of ActionViewport: " + PathOf(rect));
                Assert.That(inViewport.height,
                    Is.LessThanOrEqualTo(scroll.viewport.rect.height + tolerance),
                    matrix + " / one action is taller than ActionViewport: " + PathOf(rect));

                ProductAccessibleControl accessible =
                    control.GetComponent<ProductAccessibleControl>();
                Text visibleLabel = control.GetComponentInChildren<Text>(includeInactive: true);
                Assert.That(accessible, Is.Not.Null, matrix + " / " + PathOf(rect));
                Assert.That(visibleLabel, Is.Not.Null, matrix + " / " + PathOf(rect));
                Assert.That(accessible.ResolvedLabel, Is.Not.Empty,
                    matrix + " / runtime action accessible label: " + PathOf(rect));
                Assert.That(accessible.ResolvedLabel, Is.EqualTo(visibleLabel.text),
                    matrix + " / runtime action label must include its marker, action, " +
                    "and reason: " + PathOf(rect));
                resolvedLabels.Add(accessible.ResolvedLabel);
            }
            Assert.That(resolvedLabels, Has.Count.GreaterThan(1),
                matrix + " / representative state must cover distinct actions");
            Assert.That(resolvedLabels.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(resolvedLabels.Count),
                matrix + " / every runtime action needs a distinguishable accessible label");
        }

        private static Rect RectRelativeTo(RectTransform rect, RectTransform relativeTo)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 minimum = relativeTo.InverseTransformPoint(corners[0]);
            Vector3 maximum = minimum;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector3 point = relativeTo.InverseTransformPoint(corners[index]);
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static void AssertRectWithin(Rect actual, Rect expected, string message)
        {
            const float tolerance = 1f;
            Assert.That(actual.xMin, Is.GreaterThanOrEqualTo(expected.xMin - tolerance),
                message + " / left");
            Assert.That(actual.xMax, Is.LessThanOrEqualTo(expected.xMax + tolerance),
                message + " / right");
            Assert.That(actual.yMin, Is.GreaterThanOrEqualTo(expected.yMin - tolerance),
                message + " / bottom");
            Assert.That(actual.yMax, Is.LessThanOrEqualTo(expected.yMax + tolerance),
                message + " / top");
        }

        private static string PathOf(Transform transform)
        {
            var parts = new Stack<string>();
            Transform? current = transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", parts);
        }

        private sealed class DeterministicProductFontHost : IProductFontHost
        {
            private readonly Font fallback;

            public DeterministicProductFontHost(Font configuredFallback) =>
                fallback = configuredFallback;

            public IReadOnlyList<string> GetInstalledFontNames() =>
                ProductLocalizationController.JapaneseFontCandidates
                    .Concat(ProductLocalizationController.EnglishFontCandidates)
                    .ToArray();

            public Font? CreateDynamicFont(string fontName, int fontSize) => fallback;

            public bool HasCharacters(Font font, string characters, int fontSize) => true;
        }

        private static string SnapshotSignature(GamePresentation snapshot)
        {
            IEnumerable<string> zones = snapshot.CardZones.Select(zone =>
                zone.Id + ":" + zone.Count + ":" + string.Join(",", zone.Cards.Select(card =>
                    ((int)card.Suit) + "-" + card.Rank)));
            IEnumerable<string> actions = snapshot.Actions.Select(action =>
                action.Id + ":" + action.Action.Kind + ":" +
                (action.Action.Card.HasValue
                    ? ((int)action.Action.Card.Value.Suit) + "-" + action.Action.Card.Value.Rank
                    : "-") + ":" + (action.Action.Value ?? "-"));
            return snapshot.CurrentPlayer + "|" + snapshot.Phase + "|" +
                string.Join("|", zones) + "|" + string.Join("|", actions);
        }

        private static string TutorialTraceSignature(TutorialTraceEntry entry)
        {
            string card = entry.Action.Card.HasValue
                ? entry.Action.Card.Value.ToString()
                : "-";
            return entry.Actor + "|" + entry.Action.Kind + "|" + card + "|" +
                (entry.Action.Value ?? "-") + "|" +
                (entry.Lesson.HasValue ? entry.Lesson.Value.ToString() : "-");
        }
    }
}
