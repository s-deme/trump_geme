#nullable enable

using System;
using System.Collections;
using TrumpLab;
using UnityEngine;

namespace TrumpLab.Product
{
    public sealed class ProductAppController : MonoBehaviour
    {
        [SerializeField] private ScreenRouter? router;
        [SerializeField] private TitleScreen? titleScreen;
        [SerializeField] private GameSettingsScreen? settingsScreen;
        [SerializeField] private MatchScreen? matchScreen;
        [SerializeField] private ResultScreen? resultScreen;

        private GameSessionController? activeSession;
        private Coroutine? cpuTurnCoroutine;

        public ScreenRouter Router => router ?? throw new InvalidOperationException(
            "Screen router is not configured.");
        public IGame? ActiveGame => activeSession?.Game;
        public GameSessionController? ActiveSession => activeSession;

        public void Configure(ScreenRouter configuredRouter, TitleScreen title,
            GameSettingsScreen settings, MatchScreen match, ResultScreen result)
        {
            router = configuredRouter;
            titleScreen = title;
            settingsScreen = settings;
            matchScreen = match;
            resultScreen = result;
        }

        private void Awake()
        {
            if (router == null || titleScreen == null || settingsScreen == null ||
                matchScreen == null || resultScreen == null)
                throw new InvalidOperationException("Product app controller is not configured.");

            titleScreen.PlayRequested += HandlePlayRequested;
            titleScreen.QuitRequested += HandleQuitRequested;
            settingsScreen.StartRequested += HandleStartRequested;
            settingsScreen.BackRequested += HandleTitleRequested;
            matchScreen.ActionRequested += HandleActionRequested;
            resultScreen.RematchRequested += HandleStartRequested;
            resultScreen.TitleRequested += HandleTitleRequested;
            router.Show(ScreenId.Title);
        }

        private void OnDestroy()
        {
            if (titleScreen != null)
            {
                titleScreen.PlayRequested -= HandlePlayRequested;
                titleScreen.QuitRequested -= HandleQuitRequested;
            }
            if (settingsScreen != null)
            {
                settingsScreen.StartRequested -= HandleStartRequested;
                settingsScreen.BackRequested -= HandleTitleRequested;
            }
            if (matchScreen != null) matchScreen.ActionRequested -= HandleActionRequested;
            if (resultScreen != null)
            {
                resultScreen.RematchRequested -= HandleStartRequested;
                resultScreen.TitleRequested -= HandleTitleRequested;
            }
            EndSession();
        }

        public void ShowResultPreview(string summary)
        {
            if (resultScreen == null) throw new InvalidOperationException(
                "Result screen is not configured.");
            resultScreen.SummaryLabel.text = summary;
            Router.Show(ScreenId.Result);
        }

        private void HandlePlayRequested() => Router.Show(ScreenId.GameSettings);
        private void HandleStartRequested()
        {
            if (matchScreen == null) throw new InvalidOperationException("Match screen is not configured.");
            EndSession();
            var session = new GameSessionController(seed: 1, wildRank: 8, difficulty: 1);
            activeSession = session;
            session.SnapshotChanged += HandleSnapshotChanged;
            session.Finished += HandleSessionFinished;
            session.Faulted += HandleSessionFaulted;
            session.Begin();
            if (activeSession != session || session.State == MatchSessionState.Faulted) return;
            Router.Show(ScreenId.Match);
            ScheduleCpuTurn();
        }

        private void HandleTitleRequested()
        {
            EndSession();
            Router.Show(ScreenId.Title);
        }

        private void HandleActionRequested(string actionId)
        {
            GameSessionController? session = activeSession;
            if (session == null || !session.TryApplyHumanAction(actionId)) return;
            ScheduleCpuTurn();
        }

        private void HandleSnapshotChanged(GamePresentation presentation)
        {
            if (matchScreen == null || activeSession == null) return;
            matchScreen.Render(CrazyEightsMatchPresenter.Create(
                presentation,
                activeSession.State == MatchSessionState.AwaitingHuman));
        }

        private void HandleSessionFinished(GameResultPresentation result)
        {
            StopCpuTurn();
            if (resultScreen == null) return;
            string winners = result.Winners.Count == 0
                ? "No winner"
                : "Winner: " + string.Join(", ", System.Linq.Enumerable.Select(
                    result.Winners, winner => "Player " + (winner + 1)));
            resultScreen.SummaryLabel.text = winners + "\nTurns: " + result.Turns;
            Router.Show(ScreenId.Result);
        }

        private void HandleSessionFaulted(string message)
        {
            StopCpuTurn();
            Debug.LogError("Crazy Eights session stopped safely: " + message);
            Router.Show(ScreenId.Title);
        }

        private void ScheduleCpuTurn()
        {
            if (activeSession?.State != MatchSessionState.WaitingForCpu ||
                cpuTurnCoroutine != null) return;
            cpuTurnCoroutine = StartCoroutine(RunCpuTurn(activeSession));
        }

        private IEnumerator RunCpuTurn(GameSessionController session)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            cpuTurnCoroutine = null;
            if (activeSession != session || session.State != MatchSessionState.WaitingForCpu)
                yield break;
            session.TryApplyCpuAction();
            ScheduleCpuTurn();
        }

        private void StopCpuTurn()
        {
            if (cpuTurnCoroutine == null) return;
            StopCoroutine(cpuTurnCoroutine);
            cpuTurnCoroutine = null;
        }

        private void EndSession()
        {
            StopCpuTurn();
            if (activeSession == null) return;
            activeSession.SnapshotChanged -= HandleSnapshotChanged;
            activeSession.Finished -= HandleSessionFinished;
            activeSession.Faulted -= HandleSessionFaulted;
            activeSession = null;
        }
        private static void HandleQuitRequested() => Application.Quit();
    }
}
