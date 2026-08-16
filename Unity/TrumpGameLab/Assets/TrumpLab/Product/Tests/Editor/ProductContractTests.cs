#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

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
                Assert.That(presentation!.Banner.transform.parent, Is.SameAs(canvas.transform));
                Assert.That(presentation.Transition.transform.parent, Is.SameAs(canvas.transform));
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
