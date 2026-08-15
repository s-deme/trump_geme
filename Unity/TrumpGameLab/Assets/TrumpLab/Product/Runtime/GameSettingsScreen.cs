#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class GameSettingsScreen : ProductScreen
    {
        [SerializeField] private Text? summaryLabel;
        [SerializeField] private Button? startButton;
        [SerializeField] private Button? backButton;

        public override ScreenId Id => ScreenId.GameSettings;
        public Text SummaryLabel => summaryLabel ?? throw new InvalidOperationException(
            "Settings summary is not configured.");
        public event System.Action? StartRequested;
        public event System.Action? BackRequested;

        public void Configure(Text summary, Button start, Button back)
        {
            summaryLabel = summary;
            startButton = start;
            backButton = back;
        }

        private void Awake()
        {
            if (summaryLabel == null || startButton == null || backButton == null)
                throw new InvalidOperationException("Settings screen controls are not configured.");
            startButton.onClick.AddListener(HandleStart);
            backButton.onClick.AddListener(HandleBack);
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(HandleStart);
            if (backButton != null) backButton.onClick.RemoveListener(HandleBack);
        }

        private void HandleStart() => StartRequested?.Invoke();
        private void HandleBack() => BackRequested?.Invoke();
    }
}
