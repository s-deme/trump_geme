using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class CpuDifficultyObservationTests
    {
        [TestCase(CpuDifficulties.Standard)]
        [TestCase(CpuDifficulties.Easy)]
        [TestCase(CpuDifficulties.Hard)]
        public void FixedGameAndPolicySeedsProduceTheSameFullMatch(int difficulty)
        {
            for (long seed = 200; seed < 220; seed++)
            {
                IGame left = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
                IGame right = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
                var leftRandom = new DeterministicRandom(seed + 99991);
                var rightRandom = new DeterministicRandom(seed + 99991);

                for (int step = 0; step < 50000 && !left.IsTerminal; step++)
                {
                    Assert.That(right.IsTerminal, Is.False,
                        Context(difficulty, seed, step));
                    Assert.That(right.CurrentPlayer, Is.EqualTo(left.CurrentPlayer),
                        Context(difficulty, seed, step));
                    IReadOnlyList<Action> leftLegal = left.LegalActions();
                    IReadOnlyList<Action> rightLegal = right.LegalActions();
                    Assert.That(rightLegal, Is.EqualTo(leftLegal),
                        Context(difficulty, seed, step));
                    Action leftAction = left.ChooseCpuAction(
                        left.CurrentPlayer, leftRandom, difficulty);
                    Action rightAction = right.ChooseCpuAction(
                        right.CurrentPlayer, rightRandom, difficulty);
                    Assert.That(rightAction, Is.EqualTo(leftAction),
                        Context(difficulty, seed, step));
                    Assert.That(leftLegal, Does.Contain(leftAction),
                        Context(difficulty, seed, step));
                    left.Apply(leftAction);
                    right.Apply(rightAction);
                }

                Assert.That(left.IsTerminal, Is.True, Context(difficulty, seed, -1));
                Assert.That(right.IsTerminal, Is.True, Context(difficulty, seed, -1));
                Assert.That(ResultSignature(right.Result()),
                    Is.EqualTo(ResultSignature(left.Result())),
                    Context(difficulty, seed, -1));
            }
        }

        [TestCase(CpuDifficulties.Standard)]
        [TestCase(CpuDifficulties.Easy)]
        [TestCase(CpuDifficulties.Hard)]
        public void HiddenOpponentCardsAndStockOrderDoNotChangeCpuChoice(int difficulty)
        {
            for (long seed = 300; seed < 400; seed++)
            {
                IGame left = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
                IGame right = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
                int viewer = left.CurrentPlayer;
                SwapHiddenOpponentCardAndStock(right, viewer);

                var leftProvider = (IGamePresentationProvider)left;
                var rightProvider = (IGamePresentationProvider)right;
                Assert.That(PresentationSignature(rightProvider.Present(viewer)),
                    Is.EqualTo(PresentationSignature(leftProvider.Present(viewer))),
                    "presentation difficulty=" + difficulty + " seed=" + seed);
                Assert.That(right.LegalActions(viewer), Is.EqualTo(left.LegalActions(viewer)),
                    "legal difficulty=" + difficulty + " seed=" + seed);
                Assert.That(right.ChooseCpuAction(
                        viewer, new DeterministicRandom(seed + 88000), difficulty),
                    Is.EqualTo(left.ChooseCpuAction(
                        viewer, new DeterministicRandom(seed + 88000), difficulty)),
                    "CPU difficulty=" + difficulty + " seed=" + seed);
            }
        }

        private static void SwapHiddenOpponentCardAndStock(IGame game, int viewer)
        {
            IList hands = Field<IList>(game, "hands");
            IList stock = Field<IList>(game, "stock");
            int opponent = Enumerable.Range(0, game.Players).Single(player => player != viewer);
            IList opponentHand = (IList)hands[opponent]!;
            Assert.That(opponentHand.Count, Is.GreaterThan(0));
            Assert.That(stock.Count, Is.GreaterThan(2));
            object hiddenCard = opponentHand[0]!;
            opponentHand[0] = stock[0];
            stock[0] = hiddenCard;
            object stockCard = stock[1]!;
            stock[1] = stock[2];
            stock[2] = stockCard;
        }

        private static string PresentationSignature(GamePresentation presentation) =>
            presentation.Phase + "|" + presentation.CurrentPlayer + "|" +
            presentation.TurnCount + "|" + string.Join(";", presentation.CardZones.Select(zone =>
                zone.Id + ":" + zone.Visibility + ":" + zone.Count + ":" +
                string.Join(",", zone.Cards.Select(card =>
                    ((int)card.Suit) + "-" + card.Rank)))) + "|" +
            string.Join(";", presentation.Fields.Select(field => field.Id + ":" +
                field.Value.Kind + ":" + field.Value.TextValue + ":" + field.Value.SuitValue)) +
            "|" + string.Join(";", presentation.Actions.Select(action =>
                action.Id + ":" + ActionSignature(action.Action)));

        private static string ActionSignature(Action action) =>
            action.Kind + ":" +
            (action.Card.HasValue
                ? ((int)action.Card.Value.Suit) + "-" + action.Card.Value.Rank
                : "-") + ":" + action.Target + ":" + action.Value;

        private static string ResultSignature(GameResult result) =>
            string.Join(",", result.Winners) + "|" +
            string.Join(",", result.Scores) + "|" + result.Reason + "|" + result.Turns;

        private static string Context(int difficulty, long seed, int step) =>
            "difficulty=" + difficulty + " seed=" + seed + " step=" + step;

        private static T Field<T>(object source, string name)
        {
            Type? type = source.GetType();
            while (type != null)
            {
                FieldInfo? field = type.GetField(
                    name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) return (T)field.GetValue(source)!;
                type = type.BaseType;
            }
            throw new InvalidOperationException(
                source.GetType().Name + " missing field " + name);
        }
    }
}
