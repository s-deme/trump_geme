using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class StructuredPresentationContractTests
    {
        [Test]
        public void CrazyEightsPresentationSeparatesVisibleAndHiddenCardZones()
        {
            IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed: 3101);
            IGamePresentationProvider provider = Provider(game);
            int currentPlayer = game.CurrentPlayer;
            int otherPlayer = (currentPlayer + 1) % game.Players;

            GamePresentation currentView = provider.Present(currentPlayer);
            CardZonePresentation currentHand = Hand(currentView, currentPlayer);
            CardZonePresentation hiddenHand = Hand(currentView, otherPlayer);
            CardZonePresentation stock = Zone(currentView, "stock");
            CardZonePresentation discard = Zone(currentView, "discard");

            Assert.That(currentView.GameId, Is.EqualTo("crazy_eights"));
            Assert.That(currentView.Viewer, Is.EqualTo(currentPlayer));
            Assert.That(currentHand.Visibility, Is.EqualTo(CardZoneVisibility.FaceUp));
            Assert.That(currentHand.Cards, Has.Count.EqualTo(currentHand.Count));
            Assert.That(hiddenHand.Visibility, Is.EqualTo(CardZoneVisibility.FaceDown));
            Assert.That(hiddenHand.Cards, Is.Empty);
            Assert.That(stock.Visibility, Is.EqualTo(CardZoneVisibility.CountOnly));
            Assert.That(stock.Cards, Is.Empty);
            Assert.That(discard.Visibility, Is.EqualTo(CardZoneVisibility.FaceUp));
            Assert.That(discard.Cards, Has.Count.EqualTo(discard.Count));
            Assert.That(currentView.CardZones.Sum(zone => zone.Count), Is.EqualTo(52));

            GamePresentation otherView = provider.Present(otherPlayer);
            Assert.That(Hand(otherView, otherPlayer).Visibility,
                Is.EqualTo(CardZoneVisibility.FaceUp));
            Assert.That(Hand(otherView, otherPlayer).Cards,
                Has.Count.EqualTo(Hand(otherView, otherPlayer).Count));
            Assert.That(Hand(otherView, currentPlayer).Visibility,
                Is.EqualTo(CardZoneVisibility.FaceDown));
            Assert.That(Hand(otherView, currentPlayer).Cards, Is.Empty);
            Assert.That(otherView.Actions, Is.Empty);

            Assert.Throws<ArgumentException>(() => new CardZonePresentation(
                "hidden", "hand", 0, CardZoneVisibility.FaceDown, 1,
                new[] { new Card(Suit.Spades, 1) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => provider.Present(game.Players));
        }

        [Test]
        public void CrazyEightsPresentationActionsMatchLegalActionsByIndex()
        {
            int seed = FindSeed(actions =>
                actions.Any(action => action.Kind == "draw") &&
                actions.Any(action => action.Kind == "play"));
            IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
            IGamePresentationProvider provider = Provider(game);
            IReadOnlyList<TrumpLab.Action> legal = game.LegalActions();
            GamePresentation presentation = provider.Present();

            Assert.That(presentation.Actions, Has.Count.EqualTo(legal.Count));
            Assert.That(presentation.Actions.Select(action => action.Id),
                Is.EqualTo(Enumerable.Range(0, legal.Count).Select(index => "action_" + index)));
            Assert.That(presentation.Actions.Select(action => action.Action), Is.EqualTo(legal));
            Assert.That(presentation.Actions.Select(action => action.Id).Distinct().Count(),
                Is.EqualTo(legal.Count));
            Assert.That(presentation.Actions.Any(action => action.Action.Kind == "draw"), Is.True);
            Assert.That(presentation.Actions.Any(action => action.Action.Kind == "play"), Is.True);

            AssertDrawBoundary(seed);
            AssertPlayBoundary(seed);
        }

        [Test]
        public void CrazyEightsPresentationPublishesChosenStarterSuit()
        {
            int seed = FindSeed(actions =>
                actions.Count == 4 && actions.All(action => action.Kind == "choose_starter_suit"));
            IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
            IGamePresentationProvider provider = Provider(game);
            GamePresentation choosing = provider.Present();

            Assert.That(choosing.Phase, Is.EqualTo("choose_starter_suit"));
            Assert.That(choosing.Fields.Select(field => field.Id),
                Does.Not.Contain("called_suit"));
            ActionPresentation chooseHearts = choosing.Actions.Single(action =>
                action.Action.Value == Card.SuitCode(Suit.Hearts));

            game.Apply(chooseHearts.Action);
            GamePresentation playing = provider.Present();
            GameFieldPresentation calledSuit = playing.Fields.Single(field =>
                field.Id == "called_suit");

            Assert.That(playing.Phase, Is.EqualTo("play"));
            Assert.That(calledSuit.Value.Kind, Is.EqualTo(PresentationValueKind.Suit));
            Assert.That(calledSuit.Value.SuitValue, Is.EqualTo(Suit.Hearts));
            AssertActionsMatch(game, playing);
        }

        [Test]
        public void CrazyEightsPresentationIncludesTerminalResultWithoutActions()
        {
            IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed: 3102);
            var random = new DeterministicRandom(3103);
            int guard = 0;
            while (!game.IsTerminal && guard++ < 1000)
                game.Apply(game.ChooseCpuAction(game.CurrentPlayer, random));

            Assert.That(game.IsTerminal, Is.True);
            GamePresentation presentation = Provider(game).Present(0);
            GameResult result = game.Result();

            Assert.That(presentation.IsTerminal, Is.True);
            Assert.That(presentation.Actions, Is.Empty);
            Assert.That(presentation.Result, Is.Not.Null);
            Assert.That(presentation.Result!.Winners, Is.EqualTo(result.Winners));
            Assert.That(presentation.Result.Scores, Is.EqualTo(result.Scores));
            Assert.That(presentation.Result.Reason, Is.EqualTo(result.Reason));
            Assert.That(presentation.Result.Turns, Is.EqualTo(result.Turns));
        }

        [Test]
        public void CrazyEightsPresentationIsAnImmutableSnapshot()
        {
            IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed: 3104);
            IGamePresentationProvider provider = Provider(game);
            GamePresentation snapshot = provider.Present();
            int originalPlayer = snapshot.CurrentPlayer;
            Card[] originalHand = Hand(snapshot, originalPlayer).Cards.ToArray();
            Card[] originalDiscard = Zone(snapshot, "discard").Cards.ToArray();
            TrumpLab.Action selected = snapshot.Actions[0].Action;

            game.Apply(selected);

            Assert.That(snapshot.CurrentPlayer, Is.EqualTo(originalPlayer));
            Assert.That(Hand(snapshot, originalPlayer).Cards, Is.EqualTo(originalHand));
            Assert.That(Zone(snapshot, "discard").Cards, Is.EqualTo(originalDiscard));
            Assert.That(snapshot.Actions[0].Action, Is.EqualTo(selected));
            Assert.That(provider.Present(originalPlayer).TurnCount,
                Is.GreaterThan(snapshot.TurnCount));
        }

        private static void AssertDrawBoundary(int seed)
        {
            IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
            IGamePresentationProvider provider = Provider(game);
            GamePresentation before = provider.Present();
            int player = game.CurrentPlayer;
            int handCount = Hand(before, player).Count;
            ActionPresentation draw = before.Actions.Single(action => action.Action.Kind == "draw");

            game.Apply(draw.Action);
            GamePresentation after = provider.Present(player);

            Assert.That(Hand(after, player).Count, Is.EqualTo(handCount + 1));
            Assert.That(game.CurrentPlayer, Is.EqualTo((player + 1) % game.Players));
            Assert.That(after.Actions, Is.Empty);
        }

        private static void AssertPlayBoundary(int seed)
        {
            IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
            IGamePresentationProvider provider = Provider(game);
            GamePresentation before = provider.Present();
            int player = game.CurrentPlayer;
            int handCount = Hand(before, player).Count;
            ActionPresentation play = before.Actions.First(action => action.Action.Kind == "play");

            game.Apply(play.Action);
            GamePresentation after = provider.Present(player);

            Assert.That(Hand(after, player).Count, Is.EqualTo(handCount - 1));
            Assert.That(Zone(after, "discard").Cards.Last(), Is.EqualTo(play.Action.Card));
            Assert.That(after.Actions, Is.Empty);
        }

        private static void AssertActionsMatch(IGame game, GamePresentation presentation)
        {
            IReadOnlyList<TrumpLab.Action> legal = game.LegalActions();
            Assert.That(presentation.Actions.Select(action => action.Action), Is.EqualTo(legal));
        }

        private static int FindSeed(Func<IReadOnlyList<TrumpLab.Action>, bool> predicate)
        {
            for (int seed = 1; seed <= 1000; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
                IReadOnlyList<TrumpLab.Action> actions = game.LegalActions();
                if (predicate(actions)) return seed;
            }
            Assert.Fail("Fixed seed range did not produce the required Crazy Eights state.");
            return -1;
        }

        private static IGamePresentationProvider Provider(IGame game)
        {
            Assert.That(game, Is.InstanceOf<IGamePresentationProvider>());
            return (IGamePresentationProvider)game;
        }

        private static CardZonePresentation Hand(GamePresentation presentation, int player) =>
            presentation.CardZones.Single(zone =>
                zone.Role == "hand" && zone.OwnerPlayer == player);

        private static CardZonePresentation Zone(GamePresentation presentation, string id) =>
            presentation.CardZones.Single(zone => zone.Id == id);
    }
}
