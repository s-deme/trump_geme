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
        [SerializeField] private ProductErrorPanel? errorPanel;

        private GameSessionController? activeSession;
        private Coroutine? cpuTurnCoroutine;
        private GameStartRequest? lastRequest;

        public ScreenRouter Router => router ?? throw new InvalidOperationException(
            "Screen router is not configured.");
        public IGame? ActiveGame => activeSession?.Game;
        public GameSessionController? ActiveSession => activeSession;
        public ProductErrorPanel ErrorPanel => errorPanel ?? throw new InvalidOperationException(
            "Product error panel is not configured.");
        public GameStartRequest? LastRequest => lastRequest;

        public void Configure(ScreenRouter configuredRouter, TitleScreen title,
            GameSettingsScreen settings, MatchScreen match, ResultScreen result,
            ProductErrorPanel errors)
        {
            router = configuredRouter;
            titleScreen = title;
            settingsScreen = settings;
            matchScreen = match;
            resultScreen = result;
            errorPanel = errors;
        }

        private void Awake()
        {
            if (router == null || titleScreen == null || settingsScreen == null ||
                matchScreen == null || resultScreen == null || errorPanel == null)
                throw new InvalidOperationException("Product app controller is not configured.");

            titleScreen.PlayRequested += HandlePlayRequested;
            titleScreen.QuitRequested += HandleQuitRequested;
            settingsScreen.StartRequested += HandleStartRequested;
            settingsScreen.BackRequested += HandleTitleRequested;
            matchScreen.ActionRequested += HandleActionRequested;
            resultScreen.RematchRequested += HandleRematchRequested;
            resultScreen.TitleRequested += HandleTitleRequested;
            errorPanel.Dismissed += HandleErrorDismissed;
            errorPanel.Hide();
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
                resultScreen.RematchRequested -= HandleRematchRequested;
                resultScreen.TitleRequested -= HandleTitleRequested;
            }
            if (errorPanel != null) errorPanel.Dismissed -= HandleErrorDismissed;
            EndSession();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (errorPanel != null && errorPanel.gameObject.activeSelf)
            {
                HandleErrorDismissed();
                return;
            }
            if (Router.Current == ScreenId.GameSettings || Router.Current == ScreenId.Result)
                HandleTitleRequested();
        }

        private void HandlePlayRequested()
        {
            if (settingsScreen == null) return;
            Router.Show(ScreenId.GameSettings);
            settingsScreen.SetValues(lastRequest ?? new GameStartRequest(seed: 1, wildRank: 8));
        }

        private void HandleStartRequested(GameStartRequest request)
        {
            if (matchScreen == null) throw new InvalidOperationException("Match screen is not configured.");
            if (request == null) throw new ArgumentNullException(nameof(request));
            EndSession();
            ErrorPanel.Hide();
            lastRequest = request;
            var session = new GameSessionController(
                request.Seed, request.WildRank, request.Difficulty);
            activeSession = session;
            session.SnapshotChanged += HandleSnapshotChanged;
            session.Finished += HandleSessionFinished;
            session.Faulted += HandleSessionFaulted;
            session.Begin();
            if (activeSession != session || session.State == MatchSessionState.Faulted) return;
            Router.Show(ScreenId.Match);
            ScheduleCpuTurn();
        }

        private void HandleRematchRequested()
        {
            if (lastRequest == null)
            {
                ShowSafeError("No previous match settings are available for a rematch.");
                return;
            }
            HandleStartRequested(lastRequest);
        }

        private void HandleTitleRequested()
        {
            EndSession();
            if (errorPanel != null) errorPanel.Hide();
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
            resultScreen.Render(CrazyEightsResultPresenter.Create(result));
            Router.Show(ScreenId.Result);
        }

        private void HandleSessionFaulted(string message)
        {
            StopCpuTurn();
            Debug.LogError("Crazy Eights session stopped safely: " + message);
            ShowSafeError("The match stopped safely.\n" + message);
        }

        private void ShowSafeError(string message)
        {
            EndSession();
            Router.Show(ScreenId.Title);
            ErrorPanel.Show(message);
        }

        private void HandleErrorDismissed()
        {
            ErrorPanel.Hide();
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
