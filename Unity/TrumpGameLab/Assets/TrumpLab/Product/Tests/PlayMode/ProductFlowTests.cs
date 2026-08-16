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

        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            controller = Resources.FindObjectsOfTypeAll<ProductAppController>()
                .Single(candidate => candidate.gameObject.scene == SceneManager.GetActiveScene());
            store = new MemorySessionStore();
            controller.SetSessionStore(store);
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
        public IEnumerator CpuThinkingWaitIsCancelledWhenTheAppControllerIsDestroyed()
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

            UnityEngine.Object.Destroy(controller.gameObject);
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That(session.Game.TurnCount, Is.EqualTo(turns));
            Assert.That(session.Archive.Actions.Count, Is.EqualTo(actions));
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
    }
}
