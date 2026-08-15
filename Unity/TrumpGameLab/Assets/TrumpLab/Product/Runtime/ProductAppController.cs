#nullable enable

using System;
using UnityEngine;

namespace TrumpLab.Product
{
    public sealed class ProductAppController : MonoBehaviour
    {
        [SerializeField] private ScreenRouter? router;
        [SerializeField] private TitleScreen? titleScreen;
        [SerializeField] private GameSettingsScreen? settingsScreen;
        [SerializeField] private ResultScreen? resultScreen;

        public ScreenRouter Router => router ?? throw new InvalidOperationException(
            "Screen router is not configured.");

        public void Configure(ScreenRouter configuredRouter, TitleScreen title,
            GameSettingsScreen settings, ResultScreen result)
        {
            router = configuredRouter;
            titleScreen = title;
            settingsScreen = settings;
            resultScreen = result;
        }

        private void Awake()
        {
            if (router == null || titleScreen == null || settingsScreen == null || resultScreen == null)
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
        private void HandleStartRequested() => Router.Show(ScreenId.Match);
        private void HandleTitleRequested() => Router.Show(ScreenId.Title);
        private static void HandleQuitRequested() => Application.Quit();
    }
}
