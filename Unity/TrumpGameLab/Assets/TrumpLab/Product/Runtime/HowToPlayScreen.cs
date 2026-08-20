#nullable enable

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class HowToPlayScreen : ProductScreen
    {
        [SerializeField] private Text? pageIndicatorLabel;
        [SerializeField] private Text? pageTitleLabel;
        [SerializeField] private Text? pageBodyLabel;
        [SerializeField] private Text? contextLabel;
        [SerializeField] private Button? startTutorialButton;
        [SerializeField] private Button? previousButton;
        [SerializeField] private Button? nextButton;
        [SerializeField] private Button? backButton;

        private HowToPlayViewModel? model;
        private IProductText text = ProductTextCatalog.English;

        public override ScreenId Id => ScreenId.HowToPlay;
        public Text PageIndicatorLabel => Required(pageIndicatorLabel, nameof(pageIndicatorLabel));
        public Text PageTitleLabel => Required(pageTitleLabel, nameof(pageTitleLabel));
        public Text PageBodyLabel => Required(pageBodyLabel, nameof(pageBodyLabel));
        public Text ContextLabel => Required(contextLabel, nameof(contextLabel));
        public Button StartTutorialButton => Required(
            startTutorialButton, nameof(startTutorialButton));
        public Button PreviousButton => Required(previousButton, nameof(previousButton));
        public Button NextButton => Required(nextButton, nameof(nextButton));
        public Button BackButton => Required(backButton, nameof(backButton));
        public int CurrentPageIndex { get; private set; }
        public HowToPlayPage CurrentPage => model?.Pages[CurrentPageIndex] ??
            throw new InvalidOperationException("How-to-play content has not been rendered.");

        public event System.Action? BackRequested;
        public event System.Action? StartTutorialRequested;

        public void Configure(Text pageIndicator, Text pageTitle, Text pageBody, Text context,
            Button startTutorial, Button previous, Button next, Button back)
        {
            pageIndicatorLabel = pageIndicator;
            pageTitleLabel = pageTitle;
            pageBodyLabel = pageBody;
            contextLabel = context;
            startTutorialButton = startTutorial;
            previousButton = previous;
            nextButton = next;
            backButton = back;
            RefreshButtonText();
        }

        public void SetText(IProductText configuredText)
        {
            text = configuredText ?? throw new ArgumentNullException(nameof(configuredText));
            RefreshButtonText();
            if (model != null) ShowPage(CurrentPageIndex);
        }

        public void Render(HowToPlayViewModel viewModel)
        {
            model = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            ContextLabel.text = model.Context;
            ShowPage(model.InitialPageIndex);
        }

        public bool ShowPage(int index)
        {
            if (model == null || index < 0 || index >= model.Pages.Count) return false;
            CurrentPageIndex = index;
            HowToPlayPage page = model.Pages[index];
            PageIndicatorLabel.text = text.Get("rules.page_indicator", index + 1,
                model.Pages.Count);
            PageTitleLabel.text = page.Title;
            PageBodyLabel.text = page.Body;
            PreviousButton.interactable = index > 0;
            NextButton.interactable = index + 1 < model.Pages.Count;
            EventSystem.current?.SetSelectedGameObject(
                (NextButton.interactable ? NextButton : BackButton).gameObject);
            return true;
        }

        private void Awake()
        {
            StartTutorialButton.onClick.AddListener(HandleStartTutorial);
            PreviousButton.onClick.AddListener(HandlePrevious);
            NextButton.onClick.AddListener(HandleNext);
            BackButton.onClick.AddListener(HandleBack);
        }

        private void OnDestroy()
        {
            if (startTutorialButton != null)
                startTutorialButton.onClick.RemoveListener(HandleStartTutorial);
            if (previousButton != null) previousButton.onClick.RemoveListener(HandlePrevious);
            if (nextButton != null) nextButton.onClick.RemoveListener(HandleNext);
            if (backButton != null) backButton.onClick.RemoveListener(HandleBack);
        }

        private void HandleStartTutorial() => StartTutorialRequested?.Invoke();
        private void HandlePrevious() => ShowPage(CurrentPageIndex - 1);
        private void HandleNext() => ShowPage(CurrentPageIndex + 1);
        private void HandleBack() => BackRequested?.Invoke();

        private void RefreshButtonText()
        {
            SetButtonText(startTutorialButton, "tutorial.start_button");
            SetButtonText(previousButton, "rules.previous");
            SetButtonText(nextButton, "rules.next");
            SetButtonText(backButton, "common.back");
        }

        private void SetButtonText(Button? button, string key)
        {
            Text? label = button?.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text.Get(key);
        }

        private static T Required<T>(T? value, string name) where T : UnityEngine.Object =>
            value != null ? value : throw new InvalidOperationException(
                "How-to-play control is not configured: " + name);
    }
}
