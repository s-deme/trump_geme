#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator FullMatchRematchTitleAndErrorModalUseScreenControls()
        {
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
            if (session.State == MatchSessionState.WaitingForCpu)
            {
                int cpuTurns = session.Game.TurnCount;
                yield return new WaitForSecondsRealtime(0.5f);
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
            firstAction.onClick.Invoke();
            Assert.That(session.Game.TurnCount, Is.EqualTo(turnsBeforeInput + 1));

            if (session.State == MatchSessionState.WaitingForCpu)
            {
                int cpuTurns = session.Game.TurnCount;
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.That(session.Game.TurnCount, Is.GreaterThan(cpuTurns));
            }
            CompleteActiveMatchSynchronously();
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Result));
            var result = (ResultScreen)controller.Router.Get(ScreenId.Result);
            Assert.That(result.SummaryLabel.text, Does.Contain("Turns:"));
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

            CompleteActiveMatchSynchronously();
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
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(session.Game.TurnCount, Is.EqualTo(turns));
            Assert.That(session.Archive.Actions.Count, Is.EqualTo(actions));
            var rules = (HowToPlayScreen)controller.Router.Get(ScreenId.HowToPlay);
            rules.BackButton.onClick.Invoke();
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(session.Game.TurnCount, Is.GreaterThan(turns));
            Assert.That(session.State, Is.EqualTo(MatchSessionState.AwaitingHuman));

            ActiveActionButton().onClick.Invoke();
            Assert.That(session.State, Is.EqualTo(MatchSessionState.WaitingForCpu));
            turns = session.Game.TurnCount;
            actions = session.Archive.Actions.Count;

            UnityEngine.Object.Destroy(controller.gameObject);
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That(session.Game.TurnCount, Is.EqualTo(turns));
            Assert.That(session.Archive.Actions.Count, Is.EqualTo(actions));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator TutorialCompletesWithPointerAndSubmitThenCanBeRestarted()
        {
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
            int humanActions = 0;
            for (int step = 0; step < 100 && controller.ActiveTutorial != null; step++)
            {
                if (tutorial.State == TutorialSessionState.AwaitingHuman)
                {
                    string expectedActionId = tutorial.ExpectedActionId!;
                    Button expected = match.ActionRoot.GetComponentsInChildren<Button>(false)
                        .Single(button => button.name == "Action_" + expectedActionId);
                    Assert.That(expected.GetComponentInChildren<Text>(true).text,
                        Does.StartWith("★ "));
                    Assert.That(EventSystem.current.currentSelectedGameObject,
                        Is.EqualTo(expected.gameObject));
                    if (humanActions % 2 == 0) PointerClick(expected);
                    else Submit(expected);
                    humanActions++;
                }
                else if (tutorial.State == TutorialSessionState.WaitingForCpu)
                {
                    yield return new WaitForSecondsRealtime(0.45f);
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
            int turns = tutorial.Game.TurnCount;
            int actions = tutorial.Archive.Actions.Count;

            PointerClick(match.HelpButton);
            Assert.That(match.IsContextHelpVisible, Is.True);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(tutorial.Game.TurnCount, Is.EqualTo(turns));
            Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(actions));

            PointerClick(match.CloseHelpButton);
            Assert.That(match.IsContextHelpVisible, Is.False);
            PointerClick(match.TutorialExitButton);
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That(tutorial.State, Is.EqualTo(TutorialSessionState.Cancelled));
            Assert.That(tutorial.Game.TurnCount, Is.EqualTo(turns));
            Assert.That(tutorial.Archive.Actions.Count, Is.EqualTo(actions));
            Assert.That(controller.ActiveTutorial, Is.Null);
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
            Assert.That(progressStore.Load().IsTutorialCompleted(tutorial.Definition), Is.False);
        }

        private void CompleteActiveMatchSynchronously()
        {
            for (int step = 0; step < 1000 && controller.Router.Current == ScreenId.Match; step++)
            {
                GameSessionController session = controller.ActiveSession!;
                if (session.State == MatchSessionState.AwaitingHuman)
                    ActiveActionButton().onClick.Invoke();
                else if (session.State == MatchSessionState.WaitingForCpu)
                    Assert.That(session.TryApplyCpuAction(), Is.True);
                else
                    Assert.Fail("Unexpected session state: " + session.State);
            }
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Result));
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
