#nullable enable

using System.Collections;
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

        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            controller = Resources.FindObjectsOfTypeAll<ProductAppController>()
                .Single(candidate => candidate.gameObject.scene == SceneManager.GetActiveScene());
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
            Click(ScreenId.GameSettings, "StartButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Match));
            Assert.That(controller.LastRequest, Is.EqualTo(new GameStartRequest(23, 8)));
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
            Assert.That(VisibleSnapshotSignature(controller.ActiveSession!.Snapshot),
                Is.EqualTo(initialSignature));

            CompleteActiveMatchSynchronously();
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Result));
            Click(ScreenId.Result, "TitleButton");
            yield return null;
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
            Assert.That(controller.ActiveSession, Is.Null);

            controller.ErrorPanel.Show("Synthetic safe error");
            yield return null;
            Assert.That(controller.ErrorPanel.gameObject.activeSelf, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("DismissButton"));
            controller.ErrorPanel.GetComponentInChildren<Button>(true).onClick.Invoke();
            yield return null;
            Assert.That(controller.ErrorPanel.gameObject.activeSelf, Is.False);
            Assert.That(controller.Router.Current, Is.EqualTo(ScreenId.Title));
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
    }
}
