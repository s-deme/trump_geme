#nullable enable

using System;
using System.Collections.Generic;
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

        private IGame? activeGame;

        public ScreenRouter Router => router ?? throw new InvalidOperationException(
            "Screen router is not configured.");
        public IGame? ActiveGame => activeGame;

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
            if (resultScreen != null)
            {
                resultScreen.RematchRequested -= HandleStartRequested;
                resultScreen.TitleRequested -= HandleTitleRequested;
            }
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
            activeGame = BuiltInGames.Registry.Create(
                "crazy_eights",
                players: 2,
                seed: 1,
                options: new Dictionary<string, string> { ["wild_rank"] = "8" });
            if (!(activeGame is IGamePresentationProvider provider))
                throw new InvalidOperationException("Crazy Eights does not provide structured presentation.");
            matchScreen.Render(CrazyEightsMatchPresenter.Create(provider.Present(viewer: 0)));
            Router.Show(ScreenId.Match);
        }

        private void HandleTitleRequested()
        {
            activeGame = null;
            Router.Show(ScreenId.Title);
        }
        private static void HandleQuitRequested() => Application.Quit();
    }
}
