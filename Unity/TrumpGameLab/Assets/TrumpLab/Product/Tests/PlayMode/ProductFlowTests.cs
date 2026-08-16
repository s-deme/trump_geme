#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductFlowTests
    {
        private ProductAppController controller = null!;
        private MemorySessionStore store = null!;
        private MemoryProductProgressStore progressStore = null!;
        private MemoryProductSettingsStore productSettingsStore = null!;

        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            controller = Resources.FindObjectsOfTypeAll<ProductAppController>()
                .Single(candidate => candidate.gameObject.scene == SceneManager.GetActiveScene());
            store = new MemorySessionStore();
            controller.SetSessionStore(store);
            progressStore = new MemoryProductProgressStore();
            controller.SetProgressStore(progressStore);
            productSettingsStore = new MemoryProductSettingsStore(
                ProductSettings.CreateDefaults());
            controller.SetProductSettingsStore(productSettingsStore);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator FullMatchRematchTitleAndErrorModalUseScreenControls()
        {
            UseProductSettings(controller.CurrentProductSettings.WithPresentationSpeed(
                ProductPresentationSpeed.Fast));
            yield return null;
            var presentationCues = new List<ProductFeedbackKind>();
            var audioCues = new List<ProductFeedbackKind>();
            var lifecycleTrace = new List<string>();
            controller.PresentationController.CuePresented += kind =>
            {
                presentationCues.Add(kind);
                lifecycleTrace.Add("cue:" + kind);
            };
            ProductAudioController audio = controller.GetComponent<ProductAudioController>();
            audio.CuePlayed += audioCues.Add;
            controller.Router.ScreenChanged += screen =>
                lifecycleTrace.Add("screen:" + screen);

            Click(ScreenId.Title, "PlayButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.GameSettings));

            var settings = (GameSettingsScreen)controller.Router.Get(ScreenId.GameSettings);
            settings.HowToPlayButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.HowToPlay));
            var preMatchRules = (HowToPlayScreen)controller.Router.Get(ScreenId.HowToPlay);
            Assert.That(preMatchRules.CurrentPage.Id, Is.EqualTo(HowToPlayPageId.Objective));
            Assert.That(preMatchRules.PageIndicatorLabel.text, Is.EqualTo("Page 1 / 5"));
            preMatchRules.NextButton.onClick.Invoke();
            Assert.That(preMatchRules.CurrentPage.Id, Is.EqualTo(HowToPlayPageId.LegalPlay));
            preMatchRules.BackButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.GameSettings));

            settings.SeedInput.text = "23";
            settings.WildRankInput.text = "8";
            settings.DifficultyDropdown.value = settings.DifficultyDropdown.options
                .FindIndex(option => option.text == "Hard");
            Assert.That(settings.SummaryLabel.text, Does.EndWith("Difficulty: Hard"));
            Click(ScreenId.GameSettings, "StartButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            Assert.That(controller.LastRequest, Is.EqualTo(new GameStartRequest(
                23, 8, CpuDifficulties.Hard)));
            Assert.That(controller.ActiveSlotId, Is.Not.Null);
            Assert.That(store.List().Count, Is.EqualTo(1));
            Assert.That(controller.ActiveSession!.Archive.Configuration.Difficulty,
                Is.EqualTo(CpuDifficulties.Hard));
            Assert.That(store.Load(controller.ActiveSlotId!).Configuration.Difficulty,
                Is.EqualTo(CpuDifficulties.Hard));
            string initialSignature = VisibleSnapshotSignature(controller.ActiveSession!.Snapshot);

            GameSessionController session = controller.ActiveSession!;
            session.ActionApplied += record => lifecycleTrace.Add(
                "action:" + record.Actor + (record.TerminalAfter ? ":terminal" : string.Empty));
            if (session.State == MatchSessionState.WaitingForCpu)
            {
                int cpuTurns = session.Game.TurnCount;
                yield return WaitForSessionReadyOrResult(session);
                Assert.That(session.Game.TurnCount, Is.GreaterThan(cpuTurns));
            }

            Assert.That(session.State, Is.EqualTo(MatchSessionState.AwaitingHuman));
            var matchWithHelp = (MatchScreen)controller.Router.Get(ScreenId.Match);
            int turnsBeforeHelp = session.Game.TurnCount;
            matchWithHelp.HelpButton.onClick.Invoke();
            Assert.That(matchWithHelp.IsContextHelpVisible, Is.True);
            Assert.That(matchWithHelp.ContextHelpLabel.text,
                Does.Contain("Every shown action is legal"));
            ActiveActionButton().onClick.Invoke();
            Assert.That(session.Game.TurnCount, Is.EqualTo(turnsBeforeHelp));
            matchWithHelp.CloseHelpButton.onClick.Invoke();
            Assert.That(matchWithHelp.IsContextHelpVisible, Is.False);

            string beforeRules = VisibleSnapshotSignature(session.Snapshot);
            matchWithHelp.RulesButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.HowToPlay));
            var matchRules = (HowToPlayScreen)controller.Router.Get(ScreenId.HowToPlay);
            Assert.That(matchRules.CurrentPage.Id, Is.EqualTo(HowToPlayPageId.LegalPlay));
            ActiveActionButton().onClick.Invoke();
            Assert.That(VisibleSnapshotSignature(session.Snapshot), Is.EqualTo(beforeRules));
            matchRules.BackButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            Assert.That(VisibleSnapshotSignature(session.Snapshot), Is.EqualTo(beforeRules));

            Button firstAction = ActiveActionButton();
            int turnsBeforeInput = session.Game.TurnCount;
            firstAction.onClick.Invoke();
            Assert.That(matchWithHelp.IsPresentationLocked, Is.True);
            Assert.That(matchWithHelp.HelpButton.interactable, Is.True);
            Assert.That(matchWithHelp.RulesButton.interactable, Is.True);
            Assert.That(matchWithHelp.ActionRoot.GetComponentsInChildren<Button>(false),
                Has.All.Matches<Button>(button => !button.interactable));
            GameObject lockedFocus = EventSystem.current.currentSelectedGameObject;
            Assert.That(lockedFocus, Is.Not.Null);
            Assert.That(lockedFocus.activeInHierarchy, Is.True);
            Selectable lockedSelectable = lockedFocus.GetComponent<Selectable>();
            Assert.That(lockedSelectable, Is.Not.Null);
            Assert.That(lockedSelectable.IsActive() && lockedSelectable.IsInteractable(),
                Is.True, "Presentation lock must retain a visible focus target.");
            int rejectPresentationCount = presentationCues.Count(kind =>
                kind == ProductFeedbackKind.Reject);
            int rejectAudioCount = audioCues.Count(kind =>
                kind == ProductFeedbackKind.Reject);
            firstAction.onClick.Invoke();
            Assert.That(session.Game.TurnCount, Is.EqualTo(turnsBeforeInput + 1));
            Assert.That(presentationCues.Count(kind => kind == ProductFeedbackKind.Reject),
                Is.EqualTo(rejectPresentationCount),
                "A busy or stale double input must remain silent.");
            Assert.That(audioCues.Count(kind => kind == ProductFeedbackKind.Reject),
                Is.EqualTo(rejectAudioCount),
                "A busy or stale double input must not play rejection audio.");

            if (session.State == MatchSessionState.WaitingForCpu)
            {
                int cpuTurns = session.Game.TurnCount;
                yield return WaitForSessionReadyOrResult(session);
                Assert.That(session.Game.TurnCount, Is.GreaterThan(cpuTurns));
            }
            yield return CompleteActiveMatch();
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Result));
            var result = (ResultScreen)controller.Router.Get(ScreenId.Result);
            Assert.That(result.SummaryLabel.text, Does.Contain("Turns:"));
            ProductFeedbackKind resultCue = result.LastOutcome switch
            {
                ProductResultOutcome.Win => ProductFeedbackKind.Win,
                ProductResultOutcome.Loss => ProductFeedbackKind.Lose,
                _ => throw new AssertionException(
                    "Fixed seed 23 should produce a decisive Crazy Eights result.")
            };
            Assert.That(presentationCues, Does.Contain(resultCue));
            Assert.That(audioCues, Does.Contain(resultCue));
            int terminalActionIndex = lifecycleTrace.FindLastIndex(entry =>
                entry.EndsWith(":terminal", StringComparison.Ordinal));
            int resultScreenIndex = FindTraceIndex(lifecycleTrace, "screen:Result",
                terminalActionIndex + 1, lifecycleTrace.Count);
            Assert.That(terminalActionIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(resultScreenIndex, Is.GreaterThan(terminalActionIndex));
            SessionActionRecord terminalRecord = session.Archive.Actions.Last();
            int semanticCursor = terminalActionIndex;
            foreach (ProductFeedbackKind kind in
                ProductActionFeedback.ClassifyActionSequence(terminalRecord))
            {
                semanticCursor = FindTraceIndex(lifecycleTrace, "cue:" + kind,
                    semanticCursor + 1, resultScreenIndex);
                Assert.That(semanticCursor, Is.GreaterThan(terminalActionIndex),
                    kind + " must complete before the Result route.");
            }
            int outcomeCueIndex = FindTraceIndex(lifecycleTrace, "cue:" + resultCue,
                resultScreenIndex + 1, lifecycleTrace.Count);
            Assert.That(outcomeCueIndex, Is.GreaterThan(resultScreenIndex));
            AssertCpuCuePrecedesEachCpuAction(lifecycleTrace);
            Click(ScreenId.Result, "DetailsButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.HowToPlay));
            var resultDetails = (HowToPlayScreen)controller.Router.Get(ScreenId.HowToPlay);
            Assert.That(resultDetails.CurrentPage.Id, Is.EqualTo(HowToPlayPageId.Result));
            Assert.That(resultDetails.PageBodyLabel.text, Does.Contain("Current result"));
            Assert.That(resultDetails.PageBodyLabel.text, Does.Contain("Reason:"));
            resultDetails.BackButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Result));

            Click(ScreenId.Result, "RematchButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            Assert.That(controller.ActiveSession!.Archive.Configuration.Difficulty,
                Is.EqualTo(CpuDifficulties.Hard));
            Assert.That(VisibleSnapshotSignature(controller.ActiveSession!.Snapshot),
                Is.EqualTo(initialSignature));

            yield return CompleteActiveMatch();
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Result));
            Click(ScreenId.Result, "TitleButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
            Assert.That(controller.ActiveSession, Is.Null);

            Click(ScreenId.Title, "SessionsButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.SessionLibrary));
            var library = (SessionLibraryScreen)controller.Router.Get(ScreenId.SessionLibrary);
            Assert.That(library.SelectedSlotId, Is.Not.Null);

            Click(ScreenId.SessionLibrary, "ReplayButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Replay));
            var replay = (ReplayScreen)controller.Router.Get(ScreenId.Replay);
            Assert.That(replay.StatusLabel.text, Does.StartWith("Replayed "));
            Assert.That(replay.TableLabel.text, Does.Contain("CPU hand:"));
            Click(ScreenId.Replay, "BackButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.SessionLibrary));

            Click(ScreenId.SessionLibrary, "ResumeButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Result));
            Click(ScreenId.Result, "TitleButton");
            yield return null;
            Click(ScreenId.Title, "SessionsButton");
            yield return null;
            int slotsBeforeDelete = store.List().Count;
            Click(ScreenId.SessionLibrary, "DeleteButton");
            Assert.That(store.List().Count, Is.EqualTo(slotsBeforeDelete));
            Click(ScreenId.SessionLibrary, "DeleteButton");
            yield return null;
            Assert.That(store.List().Count, Is.EqualTo(slotsBeforeDelete - 1));

            controller.ErrorPanel.Show("Synthetic safe error");
            yield return null;
            Assert.That(presentationCues, Does.Contain(ProductFeedbackKind.Error));
            Assert.That(audioCues, Does.Contain(ProductFeedbackKind.Error));
            Assert.That(controller.ErrorPanel.gameObject.activeSelf, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("DismissButton"));
            controller.ErrorPanel.GetComponentInChildren<Button>(true).onClick.Invoke();
            yield return null;
            Assert.That(controller.ErrorPanel.gameObject.activeSelf, Is.False);
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ContextHelpPausesCpuAndDestroyCancelsTheResumedWait()
        {
            long seed = FindCpuOpeningSeed();
            Click(ScreenId.Title, "PlayButton");
            yield return null;
            var settings = (GameSettingsScreen)controller.Router.Get(ScreenId.GameSettings);
            settings.SetValues(new GameStartRequest(
                seed, wildRank: 8, difficulty: CpuDifficulties.Easy));
            Click(ScreenId.GameSettings, "StartButton");
            yield return null;

            GameSessionController session = controller.ActiveSession!;
            Assert.That(session.State, Is.EqualTo(MatchSessionState.WaitingForCpu));
            var match = (MatchScreen)controller.Router.Get(ScreenId.Match);
            Assert.That(match.StatusLabel.text, Does.StartWith("CPU is choosing"));
            int turns = session.Game.TurnCount;
            int actions = session.Archive.Actions.Count;

            match.RulesButton.onClick.Invoke();
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.HowToPlay));
            yield return AssertSessionStable(session, 0.5f);
            Assert.That(session.Game.TurnCount, Is.EqualTo(turns));
            Assert.That(session.Archive.Actions.Count, Is.EqualTo(actions));
            var rules = (HowToPlayScreen)controller.Router.Get(ScreenId.HowToPlay);
            rules.BackButton.onClick.Invoke();
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            yield return WaitForSessionReadyOrResult(session);
            Assert.That(session.Game.TurnCount, Is.GreaterThan(turns));
            Assert.That(session.State, Is.EqualTo(MatchSessionState.AwaitingHuman));

            ActiveActionButton().onClick.Invoke();
            Assert.That(session.State, Is.EqualTo(MatchSessionState.WaitingForCpu));
            yield return WaitForCondition(
                () => session.State == MatchSessionState.WaitingForCpu &&
                    !match.IsPresentationLocked,
                "Human action presentation did not release the CPU wait.");
            turns = session.Game.TurnCount;
            actions = session.Archive.Actions.Count;

            UnityEngine.Object.Destroy(controller.gameObject);
            yield return null;
            yield return AssertSessionStable(session, 0.5f);

            Assert.That(session.Game.TurnCount, Is.EqualTo(turns));
            Assert.That(session.Archive.Actions.Count, Is.EqualTo(actions));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator TutorialCompletesWithPointerAndSubmitThenCanBeRestarted()
        {
            UseProductSettings(controller.CurrentProductSettings.WithPresentationSpeed(
                ProductPresentationSpeed.Fast));
            yield return null;
            var presentationCues = new List<ProductFeedbackKind>();
            var audioCues = new List<ProductFeedbackKind>();
            controller.PresentationController.CuePresented += presentationCues.Add;
            controller.GetComponent<ProductAudioController>().CuePlayed += audioCues.Add;

            var title = (TitleScreen)controller.Router.Get(ScreenId.Title);
            Assert.That(title.TutorialCompleted, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(title.TutorialButton.gameObject));

            Submit(title.TutorialButton);
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            Assert.That(controller.ActiveSession, Is.Null);
            TutorialSessionController tutorial = controller.ActiveTutorial!;
            var match = (MatchScreen)controller.Router.Get(ScreenId.Match);
            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.AwaitingIntro));
            Assert.That(match.IsTutorialVisible, Is.True);
            Assert.That(match.TutorialProgressLabel.text, Is.EqualTo("Step 1 / 6"));
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(match.TutorialContinueButton.gameObject));

            Submit(match.TutorialContinueButton);
            yield return WaitForTutorialReady(tutorial);
            int humanActions = 0;
            bool explicitRejectionObserved = false;
            for (int step = 0; step < 100 && controller.ActiveTutorial != null; step++)
            {
                if (tutorial.State == TutorialSessionState.AwaitingHuman)
                {
                    string expectedActionId = tutorial.ExpectedActionId!;
                    Button expected = match.ActionRoot.GetComponentsInChildren<Button>(false)
                        .Single(button => button.name == "Action_" + expectedActionId);
                    if (!explicitRejectionObserved)
                    {
                        Button? alternative = match.ActionRoot
                            .GetComponentsInChildren<Button>(false)
                            .FirstOrDefault(button => button != expected);
                        if (alternative != null)
                        {
                            int actionCount = tutorial.Archive.Actions.Count;
                            int presentationCount = presentationCues.Count;
                            int audioCount = audioCues.Count;
                            PointerClick(alternative);
                            Assert.That(tutorial.Archive.Actions.Count,
                                Is.EqualTo(actionCount));
                            Assert.That(presentationCues.Skip(presentationCount),
                                Is.EqualTo(new[] { ProductFeedbackKind.Reject }));
                            Assert.That(audioCues.Skip(audioCount),
                                Is.EqualTo(new[] { ProductFeedbackKind.Reject }));
                            explicitRejectionObserved = true;
                            expected = match.ActionRoot.GetComponentsInChildren<Button>(false)
                                .Single(button => button.name == "Action_" + expectedActionId);
                        }
                    }
                    Assert.That(expected.GetComponentInChildren<Text>(true).text,
                        Does.StartWith("★ "));
                    Assert.That(EventSystem.current.currentSelectedGameObject,
                        Is.EqualTo(expected.gameObject));
                    int presentationStart = presentationCues.Count;
                    int audioStart = audioCues.Count;
                    if (humanActions % 2 == 0) PointerClick(expected);
                    else Submit(expected);
                    SessionActionRecord applied = tutorial.Archive.Actions.Last();
                    ProductFeedbackKind firstSemantic =
                        ProductActionFeedback.ClassifyActionSequence(applied)[0];
                    Assert.That(presentationCues.Skip(presentationStart),
                        Is.EqualTo(new[] { firstSemantic }),
                        "Accepted Match actions must not emit a generic Submit cue.");
                    Assert.That(audioCues.Skip(audioStart),
                        Is.EqualTo(new[] { firstSemantic }),
                        "Accepted Match actions must dispatch one immediate semantic SFX.");
                    humanActions++;
                    yield return WaitForTutorialReady(tutorial);
                }
                else if (tutorial.State == TutorialSessionState.WaitingForCpu)
                {
                    yield return WaitForTutorialReady(tutorial);
                }
                else if (tutorial.State == TutorialSessionState.AwaitingResultConfirmation)
                {
                    Assert.That(match.TutorialProgressLabel.text, Is.EqualTo("Step 6 / 6"));
                    Assert.That(match.TutorialGuidanceLabel.text, Does.Contain("Reason: empty hand"));
                    Submit(match.TutorialContinueButton);
                    yield return null;
                }
                else
                {
                    Assert.Fail("Unexpected tutorial state: " + tutorial.State + " " +
                        tutorial.FaultMessage);
                }
            }

            Assert.That(humanActions, Is.GreaterThan(0));
            Assert.That(explicitRejectionObserved, Is.True);
            ProductFeedbackKind[] expectedActionCues =
            {
                ProductFeedbackKind.CardPlay,
                ProductFeedbackKind.Draw,
                ProductFeedbackKind.WildSuit,
                ProductFeedbackKind.CpuTurn
            };
            Assert.That(presentationCues, Is.SupersetOf(expectedActionCues));
            Assert.That(audioCues, Is.SupersetOf(expectedActionCues));
            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.Finished));
            Assert.That(controller.ActiveTutorial, Is.Null);
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
            Assert.That(store.List(), Is.Empty, "Tutorial sessions must not be autosaved.");
            Assert.That(progressStore.Load().IsTutorialCompleted(tutorial.Definition), Is.True);
            Assert.That(title.TutorialCompleted, Is.True);
            Assert.That(title.TutorialButton.GetComponentInChildren<Text>(true).text,
                Is.EqualTo("How to play"));
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(title.PlayButton.gameObject));

            PointerClick(title.TutorialButton);
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.HowToPlay));
            var rules = (HowToPlayScreen)controller.Router.Get(ScreenId.HowToPlay);
            PointerClick(rules.StartTutorialButton);
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            Assert.That(controller.ActiveTutorial, Is.Not.Null);
            PointerClick(match.TutorialExitButton);
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
            Assert.That(progressStore.Load().IsTutorialCompleted(tutorial.Definition), Is.True);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator TutorialHelpPausesCpuAndExitCancelsThePendingAction()
        {
            var title = (TitleScreen)controller.Router.Get(ScreenId.Title);
            PointerClick(title.TutorialButton);
            yield return null;
            var match = (MatchScreen)controller.Router.Get(ScreenId.Match);
            PointerClick(match.TutorialContinueButton);
            TutorialSessionController tutorial = controller.ActiveTutorial!;
            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.AwaitingHuman));

            Button expected = match.ActionRoot.GetComponentsInChildren<Button>(false)
                .Single(button => button.name == "Action_" + tutorial.ExpectedActionId);
            PointerClick(expected);
            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.WaitingForCpu));
            yield return WaitForCondition(
                () => tutorial.State == TutorialSessionState.WaitingForCpu &&
                    !match.IsPresentationLocked,
                "Tutorial action presentation did not release the CPU wait.");
            int turns = tutorial.Game.TurnCount;
            int actions = tutorial.Archive.Actions.Count;

            PointerClick(match.HelpButton);
            Assert.That(match.IsContextHelpVisible, Is.True);
            yield return AssertTutorialStable(tutorial, 0.5f);
            Assert.That(tutorial.Game.TurnCount, Is.EqualTo(turns));
            Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(actions));

            PointerClick(match.CloseHelpButton);
            Assert.That(match.IsContextHelpVisible, Is.False);
            PointerClick(match.TutorialExitButton);
            yield return AssertTutorialStable(tutorial, 0.5f);

            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.Cancelled));
            Assert.That(tutorial.Game.TurnCount, Is.EqualTo(turns));
            Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(actions));
            Assert.That(controller.ActiveTutorial, Is.Null);
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
            Assert.That(progressStore.Load().IsTutorialCompleted(tutorial.Definition), Is.False);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProductSettingsApplyReloadResetAndBackUseScreenControls()
        {
            ProductSettings defaults = controller.CurrentProductSettings;
            ProductAudioController audio = controller.GetComponent<ProductAudioController>();
            float editorMasterVolume = AudioListener.volume;
            var title = (TitleScreen)controller.Router.Get(ScreenId.Title);

            title.SettingsButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.ProductSettings));

            var settings = (ProductSettingsScreen)controller.Router.Get(
                ScreenId.ProductSettings);
            settings.VSyncToggle.isOn = !defaults.VSyncEnabled;
            settings.MasterVolumeSlider.value = 37f;
            settings.MusicVolumeSlider.value = 28f;
            settings.SfxVolumeSlider.value = 49f;
            int fastIndex = settings.PresentationSpeedDropdown.options.FindIndex(
                option => option.text == "Fast");
            Assert.That(fastIndex, Is.GreaterThanOrEqualTo(0));
            settings.PresentationSpeedDropdown.value = fastIndex;

            Click(ScreenId.ProductSettings, "ApplyButton");
            yield return null;

            ProductSettings expected = defaults
                .WithVSync(!defaults.VSyncEnabled)
                .WithVolumes(37, 28, 49)
                .WithPresentationSpeed(ProductPresentationSpeed.Fast);
            Assert.That(productSettingsStore.Current, Is.EqualTo(expected));
            Assert.That(controller.CurrentProductSettings, Is.EqualTo(expected));
            Assert.That(productSettingsStore.SaveCount, Is.EqualTo(1));
            Assert.That(AudioListener.volume, Is.EqualTo(editorMasterVolume).Within(0.001f),
                "Editor tests must not mutate host-wide master volume.");
            Assert.That(audio.MusicVolumePercent, Is.EqualTo(28));
            Assert.That(audio.SfxVolumePercent, Is.EqualTo(49));
            Assert.That(audio.MusicSource.volume, Is.EqualTo(0.28f).Within(0.001f));
            Assert.That(audio.SfxSource.volume, Is.EqualTo(0.49f).Within(0.001f));

            Click(ScreenId.ProductSettings, "BackButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));

            controller.SetProductSettingsStore(productSettingsStore);
            title.SettingsButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.ProductSettings));
            Assert.That(settings.VSyncToggle.isOn, Is.EqualTo(expected.VSyncEnabled));
            Assert.That(settings.MasterVolumeSlider.value,
                Is.EqualTo((float)expected.MasterVolume));
            Assert.That(settings.MusicVolumeSlider.value,
                Is.EqualTo((float)expected.MusicVolume));
            Assert.That(settings.SfxVolumeSlider.value,
                Is.EqualTo((float)expected.SfxVolume));
            Assert.That(settings.PresentationSpeedDropdown.value, Is.EqualTo(fastIndex));

            Click(ScreenId.ProductSettings, "ResetButton");
            yield return null;
            Assert.That(productSettingsStore.ResetCount, Is.EqualTo(1));
            Assert.That(productSettingsStore.Current, Is.EqualTo(defaults));
            Assert.That(controller.CurrentProductSettings, Is.EqualTo(defaults));
            Assert.That(settings.VSyncToggle.isOn, Is.EqualTo(defaults.VSyncEnabled));
            Assert.That(settings.MasterVolumeSlider.value,
                Is.EqualTo((float)defaults.MasterVolume));
            Assert.That(settings.PresentationSpeedDropdown.value,
                Is.EqualTo((int)defaults.PresentationSpeed));
            Assert.That(AudioListener.volume,
                Is.EqualTo(editorMasterVolume).Within(0.001f),
                "Player master volume is covered by the host applier contract.");
            Assert.That(audio.MusicSource.volume,
                Is.EqualTo(defaults.MusicVolume / 100f).Within(0.001f));
            Assert.That(audio.SfxSource.volume,
                Is.EqualTo(defaults.SfxVolume / 100f).Within(0.001f));

            Click(ScreenId.ProductSettings, "BackButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator PresentationReportsRouteSubmitAndErrorEvents()
        {
            ProductPresentationController presentation = controller.PresentationController;
            ProductAudioController audio = controller.GetComponent<ProductAudioController>();
            var presentationCues = new List<ProductFeedbackKind>();
            var audioCues = new List<ProductFeedbackKind>();
            int completedTransitions = 0;
            presentation.CuePresented += presentationCues.Add;
            presentation.TransitionCompleted += () => completedTransitions++;
            audio.CuePlayed += audioCues.Add;

            var title = (TitleScreen)controller.Router.Get(ScreenId.Title);
            Submit(title.SettingsButton);

            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.ProductSettings));
            Assert.That(presentation.IsTransitioning, Is.True);
            Assert.That(presentationCues, Does.Contain(ProductFeedbackKind.Submit));
            Assert.That(audioCues, Does.Contain(ProductFeedbackKind.Submit));
            yield return WaitForCondition(
                () => completedTransitions > 0 && !presentation.IsTransitioning,
                "The route transition did not complete.");

            controller.ErrorPanel.Show("Observable presentation error.");
            yield return null;

            Assert.That(presentation.LastKind, Is.EqualTo(ProductFeedbackKind.Error));
            Assert.That(audio.LastCue, Is.EqualTo(ProductFeedbackKind.Error));
            Assert.That(presentationCues, Does.Contain(ProductFeedbackKind.Error));
            Assert.That(audioCues, Does.Contain(ProductFeedbackKind.Error));
            controller.ErrorPanel.Hide();
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator PresentationSpeedsPreserveActionSaveSnapshotAndFocusLock()
        {
            ProductPresentationSpeed[] speeds =
            {
                ProductPresentationSpeed.Reduced,
                ProductPresentationSpeed.Normal,
                ProductPresentationSpeed.Fast
            };
            long seed = FindHumanOpeningSeed();
            var archives = new List<byte[]>();
            var snapshots = new List<string>();
            var initialSnapshots = new List<string>();

            foreach (ProductPresentationSpeed speed in speeds)
            {
                UseProductSettings(controller.CurrentProductSettings
                    .WithPresentationSpeed(speed));
                Click(ScreenId.Title, "PlayButton");
                yield return null;
                var gameSettings = (GameSettingsScreen)controller.Router.Get(
                    ScreenId.GameSettings);
                gameSettings.SetValues(new GameStartRequest(seed, wildRank: 8));
                Click(ScreenId.GameSettings, "StartButton");
                yield return null;

                GameSessionController session = controller.ActiveSession!;
                MatchScreen match = MatchScreen();
                Assert.That(session.State, Is.EqualTo(MatchSessionState.AwaitingHuman));
                initialSnapshots.Add(VisibleSnapshotSignature(session.Snapshot));
                int beforeActions = session.Archive.Actions.Count;
                ActiveActionButton().onClick.Invoke();

                Assert.That(session.Archive.Actions.Count, Is.EqualTo(beforeActions + 1));
                Assert.That(match.IsPresentationLocked, Is.True, speed.ToString());
                GameObject focus = EventSystem.current.currentSelectedGameObject;
                Assert.That(focus, Is.Not.Null, speed.ToString());
                Selectable focusControl = focus.GetComponent<Selectable>();
                Assert.That(focus.activeInHierarchy && focusControl != null &&
                    focusControl.IsActive() && focusControl.IsInteractable(), Is.True,
                    speed + " must retain an active focus target during the lock.");

                // Keep a following CPU turn pending so this assertion isolates the same
                // one committed human Action at every presentation speed.
                match.ContextHelpPanel.SetActive(true);
                yield return WaitForCondition(() => !match.IsPresentationLocked,
                    speed + " did not release its presentation lock.");
                Assert.That(session.Archive.Actions.Count, Is.EqualTo(beforeActions + 1));
                SessionArchive saved = store.Load(controller.ActiveSlotId!);
                Assert.That(SessionArchiveCodec.Encode(saved),
                    Is.EqualTo(SessionArchiveCodec.Encode(session.Archive)));
                archives.Add(SessionArchiveCodec.Encode(session.Archive));
                snapshots.Add(VisibleSnapshotSignature(session.Snapshot));

                match.ContextHelpPanel.SetActive(false);
                ExecuteEvents.Execute(match.gameObject,
                    new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
                yield return null;
                Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
                Assert.That(controller.ActiveSession, Is.Null);
            }

            Assert.That(initialSnapshots.Distinct().Count(), Is.EqualTo(1));
            Assert.That(snapshots.Distinct().Count(), Is.EqualTo(1));
            Assert.That(archives.Skip(1).All(archive => archive.SequenceEqual(archives[0])),
                Is.True, "Presentation speed must not enter the saved archive.");
        }

        [UnityTest]
        public IEnumerator ProductControllerRunsDisplayGuardWithoutChangingGameState()
        {
            var guard = new RecordingDisplayGuard();
            ProductSettings before = controller.CurrentProductSettings;

            controller.SetProductSettingsApplier(guard);
            yield return null;

            Assert.That(guard.ApplyCount, Is.EqualTo(1));
            Assert.That(guard.MaintainCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(guard.LastSettings, Is.EqualTo(before));
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
        }

        [UnityTest]
        public IEnumerator GamepadReconnectDoesNotMoveFocusBehindAnotherErrorModal()
        {
            ProductErrorPanel panel = controller.ErrorPanel;
            panel.Show("A different recoverable error.");
            yield return null;
            GameObject selectedBefore = EventSystem.current.currentSelectedGameObject;
            Assert.That(selectedBefore.transform.IsChildOf(panel.transform), Is.True);
            Gamepad? gamepad = null;
            try
            {
                gamepad = InputSystem.AddDevice<Gamepad>();
                yield return null;

                Assert.That(panel.gameObject.activeSelf, Is.True);
                Assert.That(panel.MessageLabel.text, Is.EqualTo("A different recoverable error."));
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.SameAs(selectedBefore));
            }
            finally
            {
                if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
                panel.Hide();
            }
        }

        [UnityTest]
        public IEnumerator ErrorModalTrapsFocusAndRestoresBackgroundControls()
        {
            ProductErrorPanel panel = controller.ErrorPanel;
            var title = (TitleScreen)controller.Router.Get(ScreenId.Title);
            title.SettingsButton.interactable = false;
            Selectable[] backgroundControls = title.GetComponentsInChildren<Selectable>(false);
            var originalStates = backgroundControls.ToDictionary(
                control => control, control => control.interactable);
            EventSystem.current.SetSelectedGameObject(title.PlayButton.gameObject);

            panel.Show("A recoverable modal error.");
            yield return null;

            Assert.That(backgroundControls, Has.All.Matches<Selectable>(
                control => !control.interactable));
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected.transform.IsChildOf(panel.transform), Is.True);
            Button dismiss = selected.GetComponent<Button>();
            Assert.That(dismiss, Is.Not.Null);
            Assert.That(dismiss.navigation.mode, Is.EqualTo(Navigation.Mode.None));

            var move = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Down,
                moveVector = Vector2.down
            };
            ExecuteEvents.Execute(selected, move, ExecuteEvents.moveHandler);
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject.transform
                .IsChildOf(panel.transform), Is.True);

            EventSystem.current.SetSelectedGameObject(title.PlayButton.gameObject);
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject.transform
                .IsChildOf(panel.transform), Is.True);

            panel.Hide();
            yield return null;

            foreach (KeyValuePair<Selectable, bool> state in originalStates)
                Assert.That(state.Key.interactable, Is.EqualTo(state.Value), state.Key.name);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(title.PlayButton.gameObject));
            title.SettingsButton.interactable = true;
        }

        private IEnumerator CompleteActiveMatch()
        {
            GameSessionController session = controller.ActiveSession!;
            float deadline = Time.realtimeSinceStartup + 45f;
            while (controller.Router.Current == ScreenId.Match &&
                Time.realtimeSinceStartup < deadline)
            {
                Assert.That(controller.ActiveSession, Is.SameAs(session));
                var match = (MatchScreen)controller.Router.Get(ScreenId.Match);
                if (!match.IsPresentationLocked &&
                    session.State == MatchSessionState.AwaitingHuman)
                {
                    int actionCount = session.Archive.Actions.Count;
                    ActiveActionButton().onClick.Invoke();
                    Assert.That(session.Archive.Actions.Count, Is.EqualTo(actionCount + 1));
                }
                else if (session.State != MatchSessionState.WaitingForCpu &&
                    session.State != MatchSessionState.Finished &&
                    session.State != MatchSessionState.AwaitingHuman)
                {
                    Assert.Fail("Unexpected session state: " + session.State + " " +
                        session.FaultMessage);
                }
                yield return null;
            }
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Result));
        }

        private IEnumerator WaitForSessionReadyOrResult(GameSessionController session) =>
            WaitForCondition(
                () => controller.Router.Current == ScreenId.Result ||
                    (controller.ActiveSession == session &&
                     session.State == MatchSessionState.AwaitingHuman &&
                     !MatchScreen().IsPresentationLocked),
                "The session did not reach an unlocked human turn or the result screen.");

        private IEnumerator WaitForTutorialReady(TutorialSessionController tutorial) =>
            WaitForCondition(
                () => controller.ActiveTutorial != tutorial ||
                    (!MatchScreen().IsPresentationLocked &&
                     (tutorial.State == TutorialSessionState.AwaitingHuman ||
                      tutorial.State == TutorialSessionState.AwaitingResultConfirmation ||
                      tutorial.State == TutorialSessionState.Finished ||
                      tutorial.State == TutorialSessionState.Cancelled ||
                      tutorial.State == TutorialSessionState.Faulted)),
                "The tutorial did not reach its next unlocked checkpoint.");

        private static IEnumerator WaitForCondition(Func<bool> condition, string failure,
            float timeoutSeconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, failure);
        }

        private static IEnumerator AssertSessionStable(GameSessionController session,
            float durationSeconds)
        {
            int turn = session.Game.TurnCount;
            int actions = session.Archive.Actions.Count;
            float deadline = Time.realtimeSinceStartup + durationSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                Assert.That(session.Game.TurnCount, Is.EqualTo(turn));
                Assert.That(session.Archive.Actions.Count, Is.EqualTo(actions));
                yield return null;
            }
        }

        private static IEnumerator AssertTutorialStable(TutorialSessionController tutorial,
            float durationSeconds)
        {
            int turn = tutorial.Game.TurnCount;
            int actions = tutorial.Archive.Actions.Count;
            float deadline = Time.realtimeSinceStartup + durationSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                Assert.That(tutorial.Game.TurnCount, Is.EqualTo(turn));
                Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(actions));
                yield return null;
            }
        }

        private MatchScreen MatchScreen() =>
            (MatchScreen)controller.Router.Get(ScreenId.Match);

        private void UseProductSettings(ProductSettings settings)
        {
            productSettingsStore = new MemoryProductSettingsStore(settings);
            controller.SetProductSettingsStore(productSettingsStore);
        }

        private static int FindTraceIndex(IReadOnlyList<string> trace, string entry,
            int startIndex, int endExclusive)
        {
            for (int index = Math.Max(0, startIndex);
                index < Math.Min(trace.Count, endExclusive); index++)
            {
                if (trace[index] == entry) return index;
            }
            return -1;
        }

        private static void AssertCpuCuePrecedesEachCpuAction(
            IReadOnlyList<string> trace)
        {
            int previousAction = -1;
            int cpuActions = 0;
            for (int index = 0; index < trace.Count; index++)
            {
                if (!trace[index].StartsWith("action:", StringComparison.Ordinal)) continue;
                if (trace[index].StartsWith("action:1", StringComparison.Ordinal))
                {
                    cpuActions++;
                    int cue = FindTraceIndex(trace, "cue:" + ProductFeedbackKind.CpuTurn,
                        previousAction + 1, index);
                    Assert.That(cue, Is.GreaterThan(previousAction),
                        "Each CPU Action must follow its CPU-turn cue.");
                }
                previousAction = index;
            }
            Assert.That(cpuActions, Is.GreaterThan(0));
        }

        private static long FindCpuOpeningSeed()
        {
            for (long seed = 1; seed <= 100; seed++)
            {
                var session = new GameSessionController(
                    seed, wildRank: 8, difficulty: CpuDifficulties.Easy);
                session.Begin();
                if (session.State == MatchSessionState.WaitingForCpu) return seed;
            }
            Assert.Fail("Fixed seed search did not find a CPU opening turn.");
            return -1;
        }

        private static long FindHumanOpeningSeed()
        {
            for (long seed = 1; seed <= 100; seed++)
            {
                var session = new GameSessionController(
                    seed, wildRank: 8, difficulty: CpuDifficulties.Standard);
                session.Begin();
                if (session.State == MatchSessionState.AwaitingHuman) return seed;
            }
            Assert.Fail("Fixed seed search did not find a human opening turn.");
            return -1;
        }

        private Button ActiveActionButton()
        {
            var match = (MatchScreen)controller.Router.Get(ScreenId.Match);
            Button[] activeButtons = match.ActionRoot.GetComponentsInChildren<Button>(false);
            Assert.That(activeButtons, Is.Not.Empty);
            return activeButtons[0];
        }

        private void Click(ScreenId screenId, string buttonName)
        {
            ProductScreen screen = controller.Router.Get(screenId);
            Button button = screen.GetComponentsInChildren<Button>(true)
                .Single(candidate => candidate.name == buttonName);
            Assert.That(button.interactable, Is.True, buttonName);
            button.onClick.Invoke();
        }

        private static void Submit(Button button)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject,
                new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        private static void PointerClick(Button button)
        {
            var data = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        private static string VisibleSnapshotSignature(GamePresentation snapshot) =>
            snapshot.CurrentPlayer + "|" + snapshot.Phase + "|" + string.Join("|",
                snapshot.CardZones.Select(zone => zone.Id + ":" + zone.Count + ":" +
                    string.Join(",", zone.Cards.Select(card =>
                        ((int)card.Suit) + "-" + card.Rank))));

        private sealed class MemoryProductSettingsStore : IProductSettingsStore
        {
            public ProductSettings Current { get; private set; }
            public int SaveCount { get; private set; }
            public int ResetCount { get; private set; }

            public MemoryProductSettingsStore(ProductSettings initial) => Current = initial;

            public ProductSettingsLoadResult Load() => new ProductSettingsLoadResult(
                ProductSettingsLoadStatus.Loaded, Current, error: null);

            public ProductSettingsSaveResult Save(ProductSettings settings)
            {
                Current = settings;
                SaveCount++;
                return ProductSettingsSaveResult.Success();
            }

            public ProductSettingsSaveResult Reset(ProductSettings defaults)
            {
                Current = defaults;
                ResetCount++;
                return ProductSettingsSaveResult.Success();
            }
        }

        private sealed class RecordingDisplayGuard : IProductSettingsApplier,
            IProductDisplayGuard
        {
            public int ApplyCount { get; private set; }
            public int MaintainCount { get; private set; }
            public ProductSettings? LastSettings { get; private set; }

            public void Apply(ProductSettings settings)
            {
                ApplyCount++;
                LastSettings = settings;
            }

            public void MaintainValidDisplay(ProductSettings settings)
            {
                MaintainCount++;
                LastSettings = settings;
            }
        }

        private sealed class MemorySessionStore : ISessionStore
        {
            private readonly Dictionary<string, SessionArchive> archives =
                new Dictionary<string, SessionArchive>();
            private readonly Dictionary<string, System.DateTime> saved =
                new Dictionary<string, System.DateTime>();

            public IReadOnlyList<SessionSlotInfo> List() => archives.Keys
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .Select(id => new SessionSlotInfo(id, saved[id]))
                .ToArray();

            public SessionArchive Load(string slotId) => archives[SessionSlotIds.Require(slotId)];

            public void Save(string slotId, SessionArchive archive)
            {
                string id = SessionSlotIds.Require(slotId);
                archives[id] = archive;
                saved[id] = System.DateTime.UtcNow;
            }

            public void Delete(string slotId)
            {
                string id = SessionSlotIds.Require(slotId);
                archives.Remove(id);
                saved.Remove(id);
            }
        }

        private sealed class MemoryProductProgressStore : IProductProgressStore
        {
            private ProductProgress progress = ProductProgress.Empty;

            public ProductProgress Load() => progress;

            public void SaveTutorialCompleted(TutorialDefinition definition)
            {
                progress = new ProductProgress(
                    ProductProgress.CurrentFormatVersion,
                    definition.Id,
                    definition.Version,
                    tutorialCompleted: true);
            }
        }
    }
}
