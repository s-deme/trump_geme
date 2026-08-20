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
                ProductSettings.CreateDefaults("en-US"));
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
        [Timeout(15000)]
        public IEnumerator ContextHelpTrapsFocusAndRestoresEveryBackgroundControlState()
        {
            long seed = FindHumanOpeningSeed();
            Click(ScreenId.Title, "PlayButton");
            yield return null;
            var settings = (GameSettingsScreen)controller.Router.Get(ScreenId.GameSettings);
            settings.SetValues(new GameStartRequest(
                seed, wildRank: 8, difficulty: CpuDifficulties.Standard));
            Click(ScreenId.GameSettings, "StartButton");

            GameSessionController session = controller.ActiveSession!;
            yield return WaitForSessionReadyOrResult(session);
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            MatchScreen match = MatchScreen();
            Assert.That(match.IsPresentationLocked, Is.False);
            Button[] actions = match.ActionRoot.GetComponentsInChildren<Button>(false);
            Assert.That(actions, Is.Not.Empty);
            Assert.That(actions, Has.All.Matches<Button>(button => button.interactable));

            // Preserve heterogeneous state so closing the modal cannot merely enable all.
            match.RulesButton.interactable = false;
            if (actions.Length > 1) actions[actions.Length - 1].interactable = false;
            Button[] background = new[]
                {
                    match.HelpButton,
                    match.SettingsButton,
                    match.RulesButton
                }
                .Concat(actions)
                .ToArray();
            var statesBefore = background.ToDictionary(
                button => button, button => button.interactable);
            Button originalFocus = actions.First(button => button.interactable);
            EventSystem.current.SetSelectedGameObject(originalFocus.gameObject);
            AssertVisibleAccessibleFocus();

            match.HelpButton.onClick.Invoke();
            yield return null;

            Assert.That(match.IsContextHelpVisible, Is.True);
            foreach (Button button in background)
                Assert.That(button.interactable, Is.False, button.name);
            Assert.That(match.CloseHelpButton.interactable, Is.True);
            Selectable[] eligible = match.GetComponentsInChildren<Selectable>(false)
                .Where(control => control.IsActive() && control.IsInteractable() &&
                    control.GetComponent<ProductAccessibleControl>() is
                        { ParticipatesInNavigation: true })
                .ToArray();
            Assert.That(eligible, Is.EqualTo(new[] { match.CloseHelpButton }));
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(match.CloseHelpButton.gameObject));
            Assert.That(match.CloseHelpButton.navigation.mode,
                Is.EqualTo(Navigation.Mode.None));
            AssertVisibleAccessibleFocus();

            foreach (MoveDirection direction in new[]
                {
                    MoveDirection.Up,
                    MoveDirection.Down,
                    MoveDirection.Left,
                    MoveDirection.Right
                })
            {
                Vector2 vector = direction switch
                {
                    MoveDirection.Up => Vector2.up,
                    MoveDirection.Down => Vector2.down,
                    MoveDirection.Left => Vector2.left,
                    MoveDirection.Right => Vector2.right,
                    _ => Vector2.zero
                };
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                var move = new AxisEventData(EventSystem.current)
                {
                    moveDir = direction,
                    moveVector = vector
                };
                ExecuteEvents.Execute(selected, move, ExecuteEvents.moveHandler);
                yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.SameAs(match.CloseHelpButton.gameObject), direction.ToString());
                Assert.That(EventSystem.current.currentSelectedGameObject.transform
                    .IsChildOf(match.ContextHelpPanel.transform), Is.True,
                    direction.ToString());
            }

            match.CloseHelpButton.onClick.Invoke();
            yield return null;

            Assert.That(match.IsContextHelpVisible, Is.False);
            foreach (KeyValuePair<Button, bool> state in statesBefore)
                Assert.That(state.Key.interactable, Is.EqualTo(state.Value), state.Key.name);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(originalFocus.gameObject));
            Assert.That(originalFocus.IsActive() && originalFocus.IsInteractable(), Is.True);
            AssertVisibleAccessibleFocus();
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator ErrorModalComposesWithPresentationUnlockAndSuppressesGlobalHelp()
        {
            long seed = FindHumanOpeningSeed();
            Click(ScreenId.Title, "PlayButton");
            yield return null;
            var settings = (GameSettingsScreen)controller.Router.Get(ScreenId.GameSettings);
            settings.SetValues(new GameStartRequest(
                seed, wildRank: 8, difficulty: CpuDifficulties.Standard));
            Click(ScreenId.GameSettings, "StartButton");

            GameSessionController session = controller.ActiveSession!;
            yield return WaitForSessionReadyOrResult(session);
            MatchScreen match = MatchScreen();
            Button[] actions = match.ActionRoot.GetComponentsInChildren<Button>(false);
            Assert.That(actions, Is.Not.Empty);
            Assert.That(actions, Has.All.Matches<Button>(button => button.interactable));
            Button[] background = new[]
                {
                    match.HelpButton,
                    match.SettingsButton,
                    match.RulesButton
                }
                .Concat(actions)
                .ToArray();
            var desiredStates = background.ToDictionary(
                button => button, button => button.interactable);
            Button originalFocus = actions[0];
            EventSystem.current.SetSelectedGameObject(originalFocus.gameObject);

            match.SetPresentationLocked(true);
            Assert.That(match.IsPresentationLocked, Is.True);
            Assert.That(match.SettingsButton.interactable, Is.False);
            Assert.That(actions, Has.All.Matches<Button>(button => !button.interactable));

            ProductErrorPanel error = controller.ErrorPanel;
            error.Show("Composed modal state update.");
            yield return null;
            Button dismiss = error.GetComponentsInChildren<Button>(false).Single();
            Assert.That(error.gameObject.activeSelf, Is.True);
            Assert.That(dismiss.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(dismiss.gameObject));
            Selectable[] errorEligible = controller.AccessibilityController!.UiRoot
                .GetComponentsInChildren<Selectable>(false)
                .Where(control => control.IsActive() && control.IsInteractable())
                .ToArray();
            Assert.That(errorEligible, Is.EqualTo(new[] { dismiss }));
            AssertVisibleAccessibleFocus();

            match.ShowContextHelp();
            Assert.That(match.IsContextHelpVisible, Is.False,
                "The public help entry point must reject an external modal lock.");

            match.SetPresentationLocked(false);
            Assert.That(match.IsPresentationLocked, Is.False);
            foreach (Button button in background)
                Assert.That(button.interactable, Is.False,
                    button.name + " must stay disabled behind ErrorPanel.");
            yield return null;
            foreach (Button button in background)
                Assert.That(button.interactable, Is.False,
                    button.name + " was re-enabled while ErrorPanel remained visible.");

            int helpRequests = 0;
            System.Action observeHelp = () => helpRequests++;
            controller.InputController.HelpRequested += observeHelp;
            try
            {
                controller.InputController.RequestHelp();
                yield return null;
            }
            finally
            {
                controller.InputController.HelpRequested -= observeHelp;
            }

            Assert.That(helpRequests, Is.EqualTo(1),
                "The semantic global Help request must be raised exactly once.");
            Assert.That(error.gameObject.activeSelf, Is.True);
            Assert.That(match.IsContextHelpVisible, Is.False,
                "Global Help must not open a second modal behind ErrorPanel.");
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(dismiss.gameObject));
            foreach (Button button in background)
                Assert.That(button.interactable, Is.False, button.name);

            dismiss.onClick.Invoke();
            yield return null;

            Assert.That(error.gameObject.activeSelf, Is.False);
            Assert.That(match.IsPresentationLocked, Is.False);
            Assert.That(match.IsContextHelpVisible, Is.False);
            foreach (KeyValuePair<Button, bool> state in desiredStates)
                Assert.That(state.Key.interactable, Is.EqualTo(state.Value), state.Key.name);
            Assert.That(match.SettingsButton.interactable, Is.True);
            Assert.That(actions, Has.All.Matches<Button>(button => button.interactable));
            Assert.That(EventSystem.current.currentSelectedGameObject.transform
                .IsChildOf(error.transform), Is.False);
            AssertVisibleAccessibleFocus();
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
                        Does.StartWith(ProductTextCatalog.English.Get(
                            "match.marker_expected") + " "));
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
            ProductSettings loadedSettings = controller.CurrentProductSettings;
            ProductSettings defaults = ProductSettings.CreateDefaults();
            ProductAudioController audio = controller.GetComponent<ProductAudioController>();
            float editorMasterVolume = AudioListener.volume;
            var title = (TitleScreen)controller.Router.Get(ScreenId.Title);

            title.SettingsButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.ProductSettings));

            var settings = (ProductSettingsScreen)controller.Router.Get(
                ScreenId.ProductSettings);
            settings.VSyncToggle.isOn = !loadedSettings.VSyncEnabled;
            settings.MasterVolumeSlider.value = 37f;
            settings.MusicVolumeSlider.value = 28f;
            settings.SfxVolumeSlider.value = 49f;
            int fastIndex = settings.PresentationSpeedDropdown.options.FindIndex(
                option => option.text == "Fast");
            Assert.That(fastIndex, Is.GreaterThanOrEqualTo(0));
            settings.PresentationSpeedDropdown.value = fastIndex;

            Click(ScreenId.ProductSettings, "ApplyButton");
            yield return null;

            ProductSettings expected = loadedSettings
                .WithVSync(!loadedSettings.VSyncEnabled)
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
        [Timeout(15000)]
        public IEnumerator MatchSettingsApplyAccessibilityWithoutMutatingPausedSession()
        {
            long seed = FindCpuOpeningSeed();
            Click(ScreenId.Title, "PlayButton");
            var gameSettings = (GameSettingsScreen)controller.Router.Get(
                ScreenId.GameSettings);
            gameSettings.SetValues(new GameStartRequest(
                seed, wildRank: 8, difficulty: CpuDifficulties.Easy));
            Click(ScreenId.GameSettings, "StartButton");

            GameSessionController session = controller.ActiveSession!;
            Assert.That(session.State, Is.EqualTo(MatchSessionState.WaitingForCpu));
            MatchScreen match = MatchScreen();
            int cpuActions = 0;
            session.ActionApplied += action =>
            {
                if (action.Actor == 1) cpuActions++;
            };
            byte[] archiveBefore = SessionArchiveCodec.Encode(session.Archive);
            string snapshotBefore = VisibleSnapshotSignature(session.Snapshot);
            GamePresentation snapshotReference = session.Snapshot;
            int turnBefore = session.Game.TurnCount;
            int actionCountBefore = session.Archive.Actions.Count;
            string slotId = controller.ActiveSlotId!;

            match.SettingsButton.onClick.Invoke();
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.ProductSettings));
            yield return AssertSessionStable(session, 0.5f);

            ProductSettings expected = ApplyJapaneseAccessibilitySettings();
            yield return null;

            Assert.That(controller.ActiveSession, Is.SameAs(session));
            Assert.That(controller.ActiveTutorial, Is.Null);
            Assert.That(session.State, Is.EqualTo(MatchSessionState.WaitingForCpu));
            Assert.That(session.Game.TurnCount, Is.EqualTo(turnBefore));
            Assert.That(session.Archive.Actions.Count, Is.EqualTo(actionCountBefore));
            Assert.That(SessionArchiveCodec.Encode(session.Archive), Is.EqualTo(archiveBefore));
            Assert.That(VisibleSnapshotSignature(session.Snapshot), Is.EqualTo(snapshotBefore));
            Assert.That(session.Snapshot, Is.SameAs(snapshotReference));
            Assert.That(SessionArchiveCodec.Encode(store.Load(slotId)), Is.EqualTo(archiveBefore));
            Assert.That(cpuActions, Is.Zero);
            AssertAccessibilitySettingsApplied(expected);
            yield return AssertSessionStable(session, 0.25f);

            Click(ScreenId.ProductSettings, "BackButton");
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            yield return WaitForSessionReadyOrResult(session);

            Assert.That(controller.ActiveSession, Is.SameAs(session));
            Assert.That(session.State, Is.EqualTo(MatchSessionState.AwaitingHuman));
            Assert.That(cpuActions, Is.EqualTo(1),
                "Returning from Settings must resume exactly one pending CPU action.");
            Assert.That(session.Archive.Actions.Count, Is.EqualTo(actionCountBefore + 1));
            AssertVisibleAccessibleFocus();
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator TutorialSettingsApplyAccessibilityWithoutMutatingPausedTrace()
        {
            TitleScreen title = (TitleScreen)controller.Router.Get(ScreenId.Title);
            title.TutorialButton.onClick.Invoke();
            MatchScreen match = MatchScreen();
            TutorialSessionController tutorial = controller.ActiveTutorial!;
            match.TutorialContinueButton.onClick.Invoke();
            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.AwaitingHuman));

            int cpuActions = 0;
            tutorial.ActionApplied += action =>
            {
                if (action.Actor == 1) cpuActions++;
            };
            Button expectedAction = match.ActionRoot.GetComponentsInChildren<Button>(false)
                .Single(button => button.name == "Action_" + tutorial.ExpectedActionId);
            expectedAction.onClick.Invoke();
            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.WaitingForCpu));
            yield return WaitForCondition(
                () => tutorial.State == TutorialSessionState.WaitingForCpu &&
                    !match.IsPresentationLocked,
                "Tutorial presentation did not release its pending CPU wait.");

            byte[] archiveBefore = SessionArchiveCodec.Encode(tutorial.Archive);
            string snapshotBefore = VisibleSnapshotSignature(tutorial.Snapshot);
            GamePresentation snapshotReference = tutorial.Snapshot;
            int turnBefore = tutorial.Game.TurnCount;
            int actionCountBefore = tutorial.Archive.Actions.Count;
            int appliedBefore = tutorial.AppliedActions;
            string? expectedActionBefore = tutorial.ExpectedActionId;

            match.SettingsButton.onClick.Invoke();
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.ProductSettings));
            yield return AssertTutorialStable(tutorial, 0.5f);

            ProductSettings expected = ApplyJapaneseAccessibilitySettings();
            yield return null;

            Assert.That(controller.ActiveTutorial, Is.SameAs(tutorial));
            Assert.That(controller.ActiveSession, Is.Null);
            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.WaitingForCpu));
            Assert.That(tutorial.Game.TurnCount, Is.EqualTo(turnBefore));
            Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(actionCountBefore));
            Assert.That(tutorial.AppliedActions, Is.EqualTo(appliedBefore));
            Assert.That(tutorial.ExpectedActionId, Is.EqualTo(expectedActionBefore));
            Assert.That(SessionArchiveCodec.Encode(tutorial.Archive), Is.EqualTo(archiveBefore));
            Assert.That(VisibleSnapshotSignature(tutorial.Snapshot), Is.EqualTo(snapshotBefore));
            Assert.That(tutorial.Snapshot, Is.SameAs(snapshotReference));
            Assert.That(store.List(), Is.Empty, "Tutorial state must never enter session saves.");
            Assert.That(cpuActions, Is.Zero);
            AssertAccessibilitySettingsApplied(expected);
            yield return AssertTutorialStable(tutorial, 0.25f);

            Click(ScreenId.ProductSettings, "BackButton");
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            yield return WaitForTutorialReady(tutorial);

            Assert.That(controller.ActiveTutorial, Is.SameAs(tutorial));
            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.AwaitingHuman));
            Assert.That(cpuActions, Is.EqualTo(1),
                "Returning from Settings must resume exactly one pending tutorial CPU action.");
            Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(actionCountBefore + 1));
            AssertVisibleAccessibleFocus();
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

        [UnityTest]
        public IEnumerator DropdownRuntimeObjectsStayAccessibleInsideSafeFrameAndAreRemoved()
        {
            Click(ScreenId.Title, "SettingsButton");
            yield return null;
            var settings = (ProductSettingsScreen)controller.Router.Get(
                ScreenId.ProductSettings);

            Dropdown dropdown = settings.ResolutionDropdown;
            Assert.That(dropdown.options, Has.Count.EqualTo(7));
            EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
            dropdown.Show();
            yield return null;

            GameObject list = SceneObjectsNamed("Dropdown List").Single();
            GameObject blocker = SceneObjectsNamed("Blocker").Single();
            Transform safeFrame = controller.AccessibilityController!.SafeFrame.transform;
            Assert.That(list.transform.IsChildOf(safeFrame), Is.True);
            Assert.That(blocker.transform.IsChildOf(safeFrame), Is.True);

            Graphic[] listGraphics = list.GetComponentsInChildren<Graphic>(false);
            Assert.That(listGraphics, Is.Not.Empty);
            Assert.That(listGraphics, Has.All.Matches<Graphic>(graphic =>
                graphic.GetComponent<ProductGraphicElement>() != null),
                "Every runtime Dropdown graphic must retain its semantic role.");
            Text[] listTexts = list.GetComponentsInChildren<Text>(false);
            Assert.That(listTexts, Is.Not.Empty);
            Assert.That(listTexts, Has.All.Matches<Text>(text =>
                text.GetComponent<ProductTextElement>() != null),
                "Every runtime Dropdown label must retain its localization annotation.");

            Toggle[] items = list.GetComponentsInChildren<Toggle>(false);
            Assert.That(items, Has.Length.EqualTo(dropdown.options.Count));
            var itemSet = new HashSet<Selectable>(items.Cast<Selectable>());
            var resolvedItemLabels = new HashSet<string>(StringComparer.Ordinal);
            foreach (Toggle item in items)
            {
                ProductAccessibleControl accessible =
                    item.GetComponent<ProductAccessibleControl>();
                Assert.That(accessible, Is.Not.Null, item.name);
                Assert.That(accessible.ParticipatesInNavigation, Is.True, item.name);
                Assert.That(ProductTextCatalog.Entry(accessible.LabelKey).ArgumentCount,
                    Is.Zero, accessible.LabelKey);
                Text visibleLabel = item.GetComponentInChildren<Text>(true);
                Assert.That(visibleLabel, Is.Not.Null, item.name);
                Assert.That(accessible.ResolvedLabel,
                    Is.EqualTo(controller.Text.Get("accessibility.dropdown_option",
                        visibleLabel.text)));
                Assert.That(resolvedItemLabels.Add(accessible.ResolvedLabel), Is.True,
                    "Each visible option needs a distinguishable accessible label.");
                Assert.That(accessible.HasMinimumReferenceHitTarget, Is.True,
                    item.name + " size=" + accessible.ReferenceHitSize);
                Assert.That(item.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit),
                    item.name);
                foreach (Selectable target in NavigationTargets(item))
                    Assert.That(itemSet.Contains(target), Is.True,
                        item.name + " navigation escaped the open Dropdown list to " +
                        target.name);
                if (items.Length > 1)
                {
                    Assert.That(itemSet.Contains(item.navigation.selectOnUp) ||
                        itemSet.Contains(item.navigation.selectOnDown), Is.True,
                        item.name + " must participate in Dropdown item navigation.");
                }
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.transform.IsChildOf(list.transform), Is.True);
            ProductAccessibleControl selectedAccessible =
                selected.GetComponent<ProductAccessibleControl>();
            Assert.That(selectedAccessible, Is.Not.Null);
            Assert.That(selectedAccessible.IsFocusVisible, Is.True);

            for (int step = 0; step < items.Length + 1; step++)
            {
                GameObject current = EventSystem.current.currentSelectedGameObject;
                var move = new AxisEventData(EventSystem.current)
                {
                    moveDir = MoveDirection.Down,
                    moveVector = Vector2.down
                };
                ExecuteEvents.Execute(current, move, ExecuteEvents.moveHandler);
                yield return null;
            }
            Toggle lastItem = items[items.Length - 1];
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(lastItem.gameObject));
            ProductAccessibleControl lastAccessible =
                lastItem.GetComponent<ProductAccessibleControl>();
            Assert.That(lastAccessible.IsFocusVisible, Is.True);
            ScrollRect listScroll = list.GetComponent<ScrollRect>();
            Assert.That(listScroll, Is.Not.Null);
            Assert.That(listScroll.viewport, Is.Not.Null);
            Bounds selectedBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                listScroll.viewport, (RectTransform)lastItem.transform);
            Rect viewportRect = listScroll.viewport.rect;
            const float viewportTolerance = 1f;
            Assert.That(selectedBounds.min.x,
                Is.GreaterThanOrEqualTo(viewportRect.xMin - viewportTolerance));
            Assert.That(selectedBounds.max.x,
                Is.LessThanOrEqualTo(viewportRect.xMax + viewportTolerance));
            Assert.That(selectedBounds.min.y,
                Is.GreaterThanOrEqualTo(viewportRect.yMin - viewportTolerance));
            Assert.That(selectedBounds.max.y,
                Is.LessThanOrEqualTo(viewportRect.yMax + viewportTolerance));

            Selectable blockerControl = blocker.GetComponent<Selectable>();
            Assert.That(blockerControl, Is.Not.Null);
            Image blockerImage = blocker.GetComponent<Image>();
            Assert.That(blockerImage, Is.Not.Null);
            Assert.That(blockerImage.color.a, Is.Zero,
                "The pointer-dismiss surface must not visually cover the open screen.");
            ProductGraphicElement blockerGraphic =
                blocker.GetComponent<ProductGraphicElement>();
            Assert.That(blockerGraphic, Is.Not.Null);
            Assert.That(blockerGraphic.TargetGraphic, Is.SameAs(blockerImage));
            Assert.That(blockerGraphic.BaseRole, Is.EqualTo(ProductGraphicRole.Surface));
            Assert.That(blockerGraphic.PreserveAlpha, Is.True);
            ProductAccessibleControl blockerAccessible =
                blocker.GetComponent<ProductAccessibleControl>();
            Assert.That(blockerAccessible, Is.Not.Null);
            Assert.That(blockerAccessible.LabelKey, Is.EqualTo("common.cancel"));
            Assert.That(blockerAccessible.ResolvedLabel,
                Is.EqualTo(controller.Text.Get("common.cancel")));
            Assert.That(blockerAccessible.ParticipatesInNavigation, Is.False);
            Assert.That(blockerAccessible.IsFocusVisible, Is.False);
            Assert.That(blockerControl.navigation.mode, Is.EqualTo(Navigation.Mode.None),
                "The pointer-dismiss Blocker must not enter keyboard/gamepad navigation.");

            float fadeDuration = dropdown.alphaFadeSpeed;
            dropdown.Hide();
            yield return new WaitForSecondsRealtime(fadeDuration + 0.05f);
            yield return null;

            Assert.That(SceneObjectsNamed("Dropdown List"), Is.Empty);
            Assert.That(SceneObjectsNamed("Blocker"), Is.Empty);
            Selectable[] restoredGraph = settings.GetComponentsInChildren<Selectable>(false)
                .Where(control => control.IsActive() && control.IsInteractable() &&
                    control.GetComponent<ProductAccessibleControl>() is
                        { ParticipatesInNavigation: true })
                .ToArray();
            var restoredSet = new HashSet<Selectable>(restoredGraph);
            int restoredEdges = 0;
            foreach (Selectable control in restoredGraph)
            {
                Assert.That(control.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit),
                    control.name);
                foreach (Selectable target in NavigationTargets(control))
                {
                    restoredEdges++;
                    Assert.That(restoredSet.Contains(target), Is.True,
                        control.name + " retained an edge outside the restored screen graph.");
                }
            }
            Assert.That(restoredEdges, Is.GreaterThan(0));
            Assert.That(EventSystem.current.currentSelectedGameObject.transform
                .IsChildOf(settings.transform), Is.True);
            AssertVisibleAccessibleFocus();
        }

        [UnityTest]
        public IEnumerator PersistedJapaneseWithoutFontGlyphsUsesVisibleEnglishStartupWarning()
        {
            ProductLocalizationController localization =
                controller.LocalizationController!;
            localization.SetFontHost(new MissingFontHost());
            ProductSettings persisted = controller.CurrentProductSettings.WithLocale(
                ProductTextCatalog.JapaneseLocale);

            UseProductSettings(persisted);

            string warning = ProductTextCatalog.English.Get(
                "warning.font.japanese_fallback");
            Assert.That(productSettingsStore.Current.Locale,
                Is.EqualTo(ProductTextCatalog.JapaneseLocale));
            Assert.That(productSettingsStore.SaveCount, Is.Zero,
                "Presentation fallback must not rewrite persisted settings.");
            Assert.That(controller.CurrentProductSettings.Locale,
                Is.EqualTo(ProductTextCatalog.JapaneseLocale));
            Assert.That(localization.RequestedLocale,
                Is.EqualTo(ProductTextCatalog.JapaneseLocale));
            Assert.That(localization.EffectiveLocale,
                Is.EqualTo(ProductTextCatalog.EnglishLocale));
            Assert.That(localization.EffectiveFont, Is.SameAs(localization.FallbackFont));
            Assert.That(localization.HasCompleteGlyphCoverage, Is.False);
            Assert.That(localization.LastWarning, Is.EqualTo(warning));
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
            Assert.That(controller.ErrorPanel.gameObject.activeSelf, Is.True);
            Assert.That(controller.ErrorPanel.MessageLabel.text, Is.EqualTo(warning));

            yield return null;
            Assert.That(controller.ErrorPanel.gameObject.activeSelf, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject.transform
                .IsChildOf(controller.ErrorPanel.transform), Is.True);
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

        private ProductSettings ApplyJapaneseAccessibilitySettings()
        {
            ProductSettings before = controller.CurrentProductSettings;
            ProductSettings expected = before
                .WithLocale(ProductTextCatalog.JapaneseLocale)
                .WithTextScalePercent(150)
                .WithHighContrast(true)
                .WithReducedMotion(true);
            var settings = (ProductSettingsScreen)controller.Router.Get(
                ScreenId.ProductSettings);
            settings.AccessibilityPageButton.onClick.Invoke();
            Assert.That(settings.LocaleDropdown.options, Has.Count.EqualTo(2));
            Assert.That(settings.LocaleDropdown.options[1].text,
                Is.EqualTo(controller.Text.Get("settings.locale_ja")));
            Assert.That(settings.TextScaleDropdown.options, Has.Count.EqualTo(3));
            Assert.That(settings.TextScaleDropdown.options[2].text,
                Is.EqualTo(controller.Text.Get("settings.text_scale_value", 150)));
            settings.LocaleDropdown.SetValueWithoutNotify(1);
            settings.TextScaleDropdown.SetValueWithoutNotify(2);
            settings.HighContrastToggle.SetIsOnWithoutNotify(true);
            settings.ReducedMotionToggle.SetIsOnWithoutNotify(true);

            Click(ScreenId.ProductSettings, "ApplyButton");
            return expected;
        }

        private void AssertAccessibilitySettingsApplied(ProductSettings expected)
        {
            Assert.That(controller.CurrentProductSettings, Is.EqualTo(expected));
            Assert.That(productSettingsStore.Current, Is.EqualTo(expected));
            Assert.That(productSettingsStore.SaveCount, Is.EqualTo(1));
            ProductLocalizationController localization = controller.LocalizationController!;
            ProductAccessibilityController accessibility = controller.AccessibilityController!;
            Assert.That(localization, Is.Not.Null);
            Assert.That(accessibility, Is.Not.Null);
            Assert.That(localization.RequestedLocale,
                Is.EqualTo(ProductTextCatalog.JapaneseLocale));
            Assert.That(new[]
                {
                    ProductTextCatalog.JapaneseLocale,
                    ProductTextCatalog.EnglishLocale
                }, Does.Contain(localization.EffectiveLocale));
            if (localization.EffectiveLocale == ProductTextCatalog.EnglishLocale)
                Assert.That(localization.LastWarning, Is.Not.Null.And.Not.Empty);
            Assert.That(accessibility.CurrentPalette,
                Is.SameAs(ProductUiPalette.HighContrast));
            Assert.That(controller.PresentationController.Policy.ReducedMotion, Is.True);
            Assert.That(controller.PresentationController.Policy.MotionEnabled, Is.False);

            var settings = (ProductSettingsScreen)controller.Router.Get(
                ScreenId.ProductSettings);
            ProductTextElement title = settings.GetComponentsInChildren<ProductTextElement>(true)
                .Single(element => element.StableKey == "settings.title");
            Assert.That(title.Target.text, Is.EqualTo(controller.Text.Get("settings.title")));
            Assert.That(title.Target.fontSize,
                Is.EqualTo(Mathf.RoundToInt(title.BaseFontSize * 1.5f)));
            ProductAccessibleControl accessibilityTab =
                settings.AccessibilityPageButton.GetComponent<ProductAccessibleControl>();
            Assert.That(accessibilityTab, Is.Not.Null);
            Assert.That(accessibilityTab.ResolvedLabel,
                Is.EqualTo(controller.Text.Get(accessibilityTab.LabelKey)));
            AssertVisibleAccessibleFocus();
        }

        private static void AssertVisibleAccessibleFocus()
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.activeInHierarchy, Is.True);
            Selectable selectable = selected.GetComponent<Selectable>();
            Assert.That(selectable, Is.Not.Null);
            Assert.That(selectable.IsActive() && selectable.IsInteractable(), Is.True);
            ProductAccessibleControl accessible =
                selected.GetComponent<ProductAccessibleControl>();
            Assert.That(accessible, Is.Not.Null, selected.name);
            Assert.That(accessible.IsFocusVisible, Is.True, selected.name);
        }

        private static GameObject[] SceneObjectsNamed(string objectName)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(candidate => candidate != null &&
                    candidate.scene == activeScene && candidate.name == objectName)
                .ToArray();
        }

        private static IEnumerable<Selectable> NavigationTargets(Selectable control)
        {
            Navigation navigation = control.navigation;
            if (navigation.selectOnUp != null) yield return navigation.selectOnUp;
            if (navigation.selectOnDown != null) yield return navigation.selectOnDown;
            if (navigation.selectOnLeft != null) yield return navigation.selectOnLeft;
            if (navigation.selectOnRight != null) yield return navigation.selectOnRight;
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

        private sealed class MissingFontHost : IProductFontHost
        {
            public IReadOnlyList<string> GetInstalledFontNames() => Array.Empty<string>();

            public Font? CreateDynamicFont(string fontName, int fontSize) => null;

            public bool HasCharacters(Font font, string characters, int fontSize) => false;
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
