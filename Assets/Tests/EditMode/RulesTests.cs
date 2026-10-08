using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class TurnRotationTests
{
    private static List<PlayerLifeState> States(params PlayerLifeState[] states) => new List<PlayerLifeState>(states);

    private const PlayerLifeState A = PlayerLifeState.Alive;
    private const PlayerLifeState E = PlayerLifeState.Eliminated;
    private const PlayerLifeState D = PlayerLifeState.Disconnected;

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void RotatesThroughAllAlivePlayers(int count)
    {
        List<PlayerLifeState> states = new List<PlayerLifeState>();
        for (int i = 0; i < count; i++) states.Add(A);

        int current = 0;
        for (int i = 1; i <= count * 2; i++)
        {
            current = TurnRotation.GetNextPlayer(current, states);
            Assert.AreEqual(i % count, current);
        }
    }

    [Test]
    public void SkipsEliminatedAndDisconnected()
    {
        List<PlayerLifeState> states = States(A, E, D, A);
        Assert.AreEqual(3, TurnRotation.GetNextPlayer(0, states));
        Assert.AreEqual(0, TurnRotation.GetNextPlayer(3, states));
    }

    [Test]
    public void ActivePlayerEliminatedPassesToNext()
    {
        List<PlayerLifeState> states = States(A, E, A, A);
        Assert.AreEqual(2, TurnRotation.GetNextPlayer(1, states));
    }

    [Test]
    public void NoAlivePlayersReturnsNoPlayer()
    {
        Assert.AreEqual(TurnRotation.NoPlayer, TurnRotation.GetNextPlayer(0, States(E, D)));
    }

    [Test]
    public void FirstPlayerFallsBackWhenPreferredIsNotAlive()
    {
        Assert.AreEqual(0, TurnRotation.GetFirstPlayer(States(A, A), 0));
        Assert.AreEqual(1, TurnRotation.GetFirstPlayer(States(D, A), 0));
    }

    [Test]
    public void LastStanding()
    {
        Assert.AreEqual(2, TurnRotation.GetLastStanding(States(E, D, A, E)));
        Assert.AreEqual(TurnRotation.NoPlayer, TurnRotation.GetLastStanding(States(A, A)));
        Assert.AreEqual(2, TurnRotation.CountAlive(States(A, E, A, D)));
    }
}

public class VictoryRulesTests
{
    private const PlayerLifeState A = PlayerLifeState.Alive;
    private const PlayerLifeState E = PlayerLifeState.Eliminated;

    [Test]
    public void CompletingTheMissionWins()
    {
        int winner = VictoryRules.ResolveWinner(1, new List<bool> { false, true }, new List<PlayerLifeState> { A, A }, 2);
        Assert.AreEqual(1, winner);
    }

    [Test]
    public void NoWinnerWithoutMissionAndSeveralAlive()
    {
        int winner = VictoryRules.ResolveWinner(0, new List<bool> { false, false }, new List<PlayerLifeState> { A, A }, 2);
        Assert.AreEqual(TurnRotation.NoPlayer, winner);
    }

    [Test]
    public void TieGoesToActivePlayer()
    {
        int winner = VictoryRules.ResolveWinner(1, new List<bool> { true, true }, new List<PlayerLifeState> { A, A }, 2);
        Assert.AreEqual(1, winner);
    }

    [Test]
    public void EliminatedPlayerCannotWinByMission()
    {
        int winner = VictoryRules.ResolveWinner(0, new List<bool> { true, false, false }, new List<PlayerLifeState> { E, A, A }, 3);
        Assert.AreEqual(TurnRotation.NoPlayer, winner);
    }

    [Test]
    public void LastPlayerStandingWins()
    {
        int winner = VictoryRules.ResolveWinner(0, new List<bool> { false, false, false }, new List<PlayerLifeState> { E, A, E }, 3);
        Assert.AreEqual(1, winner);
    }

    [Test]
    public void EveryoneEliminatedHasNoWinner()
    {
        int winner = VictoryRules.ResolveWinner(0, new List<bool> { false, false }, new List<PlayerLifeState> { E, E }, 2);
        Assert.AreEqual(TurnRotation.NoPlayer, winner);
        Assert.AreEqual(0, TurnRotation.CountAlive(new List<PlayerLifeState> { E, E }));
    }

    [Test]
    public void SoloGameDoesNotWinByLastStanding()
    {
        int winner = VictoryRules.ResolveWinner(0, new List<bool> { false }, new List<PlayerLifeState> { A }, 1);
        Assert.AreEqual(TurnRotation.NoPlayer, winner);
    }
}

public class MissionCellTests
{
    private static BoardLayout.Settings Settings(int center, int room) => new BoardLayout.Settings
    {
        centerWidth = center, centerHeight = center, centerExtraPathChance = 0.35f,
        roomWidth = room, roomDepth = room, roomExtraPathChance = 0.35f,
        north = true, south = true, east = true, west = true
    };

    [TestCase(11, 7)]
    [TestCase(8, 5)]
    public void MissionCellIsAtTheBackOfItsRoomAndReachable(int center, int room)
    {
        for (int seed = 0; seed < 50; seed++)
        {
            BoardLayout layout = BoardLayout.Generate(Settings(center, room), seed);
            var reachable = layout.Reachable(new Vector2Int(center / 2, center / 2));

            foreach (BoardRegion side in RoomThemeAssignment.SideOrder)
            {
                Vector2Int cell = layout.MissionCell(side);
                Assert.IsTrue(layout.Contains(cell), $"{side} seed {seed}");
                Assert.AreEqual(side, layout.RegionOf(cell));
                Assert.IsTrue(reachable.Contains(cell));

                // Opposite the entrance: as far as possible from it inside the room.
                Vector2Int entrance = layout.Entrances[side];
                int distance = Mathf.Abs(cell.x - entrance.x) + Mathf.Abs(cell.y - entrance.y);
                Assert.AreEqual(room - 1, distance, side.ToString());
            }
        }
    }
}

public class CharacterSelectionRulesTests
{
    private static CharacterSelectionRules.SlotView Slot(int playerId, int characterId, bool ready = false) =>
        new CharacterSelectionRules.SlotView { PlayerId = playerId, CharacterId = characterId, Ready = ready, Connected = true };

    [Test]
    public void CannotPickTakenCharacter()
    {
        var slots = new List<CharacterSelectionRules.SlotView> { Slot(0, 2), Slot(1, -1) };
        Assert.AreEqual(CharacterSelectionRules.SelectResult.AlreadyTaken, CharacterSelectionRules.CanSelect(1, 2, 6, slots));
        Assert.AreEqual(CharacterSelectionRules.SelectResult.Ok, CharacterSelectionRules.CanSelect(1, 3, 6, slots));
    }

    [Test]
    public void CannotChangeWhileReadyOrInvalid()
    {
        var slots = new List<CharacterSelectionRules.SlotView> { Slot(0, 2, true) };
        Assert.AreEqual(CharacterSelectionRules.SelectResult.PlayerIsReady, CharacterSelectionRules.CanSelect(0, 3, 6, slots));
        Assert.AreEqual(CharacterSelectionRules.SelectResult.InvalidCharacter, CharacterSelectionRules.CanSelect(0, 9, 6, slots));
    }

    [Test]
    public void StartRequiresMinPlayersAllReadyDistinct()
    {
        var one = new List<CharacterSelectionRules.SlotView> { Slot(0, 1, true) };
        Assert.IsFalse(CharacterSelectionRules.CanStart(one, 2, 4));

        var notReady = new List<CharacterSelectionRules.SlotView> { Slot(0, 1, true), Slot(1, 2, false) };
        Assert.IsFalse(CharacterSelectionRules.CanStart(notReady, 2, 4));

        var duplicate = new List<CharacterSelectionRules.SlotView> { Slot(0, 1, true), Slot(1, 1, true) };
        Assert.IsFalse(CharacterSelectionRules.CanStart(duplicate, 2, 4));

        var ok = new List<CharacterSelectionRules.SlotView> { Slot(0, 1, true), Slot(1, 2, true) };
        Assert.IsTrue(CharacterSelectionRules.CanStart(ok, 2, 4));
    }

    [Test]
    public void LowestFreePlayerIdIsReused()
    {
        var slots = new List<CharacterSelectionRules.SlotView> { Slot(0, -1), Slot(2, -1) };
        Assert.AreEqual(1, CharacterSelectionRules.GetLowestFreePlayerId(slots, 4));
    }
}

public class CombatTests
{
    private static T Effect<T>(int amount = 0) where T : CardEffect
    {
        T effect = ScriptableObject.CreateInstance<T>();
        var field = typeof(T).GetField("amount");
        if (field != null) field.SetValue(effect, amount);
        return effect;
    }

    [Test]
    public void RollAtOrAboveRequiredWins()
    {
        var ctx = new CombatContext(0, 4, 1) { Roll = 4 };
        CombatOutcome outcome = CombatResolver.Resolve(ctx);
        Assert.IsTrue(outcome.Won);
        Assert.AreEqual(1, outcome.ProgressGain);
        Assert.AreEqual(0, outcome.LifeLoss);
    }

    [Test]
    public void RollBelowRequiredLosesOneLife()
    {
        var ctx = new CombatContext(0, 4, 1) { Roll = 3 };
        CombatOutcome outcome = CombatResolver.Resolve(ctx);
        Assert.IsFalse(outcome.Won);
        Assert.AreEqual(1, outcome.LifeLoss);
    }

    [Test]
    public void AddToRollTurnsDefeatIntoVictory()
    {
        var ctx = new CombatContext(0, 4, 1) { Roll = 3 };
        Effect<AddToRollEffect>(2).Apply(ctx);
        Assert.AreEqual(5, ctx.Result);
        Assert.IsTrue(CombatResolver.Resolve(ctx).Won);
    }

    [Test]
    public void ReduceRequiredLowersThreshold()
    {
        var ctx = new CombatContext(0, 4, 1) { Roll = 3 };
        Effect<ReduceRequiredEffect>(1).Apply(ctx);
        Assert.AreEqual(3, ctx.Required);
        Assert.IsTrue(CombatResolver.Resolve(ctx).Won);
    }

    [Test]
    public void PreventLifeLossAvoidsDamage()
    {
        var ctx = new CombatContext(0, 4, 1) { Roll = 1 };
        Effect<PreventLifeLossEffect>().Apply(ctx);
        CombatOutcome outcome = CombatResolver.Resolve(ctx);
        Assert.IsFalse(outcome.Won);
        Assert.AreEqual(0, outcome.LifeLoss);
    }

    [Test]
    public void ReduceDamageSubtractsFromDamage()
    {
        var ctx = new CombatContext(0, 6, 2) { Roll = 1 };
        Effect<ReduceDamageEffect>(1).Apply(ctx);
        Assert.AreEqual(1, CombatResolver.Resolve(ctx).LifeLoss);
    }

    [Test]
    public void UselessCardChangesNothing()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        var ctx = new CombatContext(0, 4, 1) { Roll = 3 };
        card.Apply(ctx);
        Assert.AreEqual(3, ctx.Result);
        Assert.AreEqual(4, ctx.Required);
        Assert.AreEqual(1, ctx.LifeLoss);
    }
}

public class CardDealTests
{
    [Test]
    public void DealsRequestedHandSize()
    {
        var hand = CardDatabase.DealHand(new List<int> { 1, 2, 3, 4 }, 3, true, new System.Random(1));
        Assert.AreEqual(3, hand.Count);
    }

    [Test]
    public void NoDuplicatesWhenDisabled()
    {
        var hand = CardDatabase.DealHand(new List<int> { 1, 2, 3 }, 3, false, new System.Random(7));
        CollectionAssert.AllItemsAreUnique(hand);
        Assert.AreEqual(2, CardDatabase.DealHand(new List<int> { 1, 2 }, 3, false, new System.Random(7)).Count);
    }
}

public class BoardLayoutTests
{
    private static BoardLayout.Settings Settings(int center = 11, int room = 7) => new BoardLayout.Settings
    {
        centerWidth = center,
        centerHeight = center,
        centerExtraPathChance = 0.35f,
        roomWidth = room,
        roomDepth = room,
        roomExtraPathChance = 0.35f,
        north = true,
        south = true,
        east = true,
        west = true
    };

    [TestCase(11, 7)]
    [TestCase(8, 5)]
    [TestCase(9, 6)]
    public void SizesAreRespected(int center, int room)
    {
        BoardLayout layout = BoardLayout.Generate(Settings(center, room), 1);
        Assert.AreEqual(center * center, layout.CellsIn(BoardRegion.Center).Count);

        foreach (BoardRegion r in new[] { BoardRegion.North, BoardRegion.South, BoardRegion.East, BoardRegion.West })
            Assert.AreEqual(room * room, layout.CellsIn(r).Count, r.ToString());

        Assert.AreEqual(center * center + 4 * room * room, layout.Cells.Count);
    }

    [Test]
    public void SameSeedSameBoard()
    {
        BoardLayout a = BoardLayout.Generate(Settings(), 12345);
        BoardLayout b = BoardLayout.Generate(Settings(), 12345);

        foreach (Vector2Int cell in a.Cells)
            Assert.AreEqual(a.WallMask(cell), b.WallMask(cell));
    }

    [Test]
    public void DifferentSeedsDiffer()
    {
        BoardLayout a = BoardLayout.Generate(Settings(), 1);
        BoardLayout b = BoardLayout.Generate(Settings(), 2);
        int differences = 0;

        foreach (Vector2Int cell in a.Cells)
            if (a.WallMask(cell) != b.WallMask(cell)) differences++;

        Assert.Greater(differences, 0);
    }

    [TestCase(11, 7)]
    [TestCase(8, 5)]
    [TestCase(9, 6)]
    public void WallsAgreeOnBothSidesAndEverythingIsReachable(int center, int room)
    {
        for (int seed = 0; seed < 200; seed++)
        {
            BoardLayout layout = BoardLayout.Generate(Settings(center, room), seed);
            Assert.AreEqual(0, layout.CountAsymmetricWalls(), "seed " + seed);

            Vector2Int start = new Vector2Int(center / 2, center / 2);
            Assert.AreEqual(layout.Cells.Count, layout.Reachable(start).Count, "seed " + seed);
        }
    }

    [Test]
    public void BorderIsClosedAndEachRoomHasExactlyOneDoor()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            BoardLayout layout = BoardLayout.Generate(Settings(), seed);
            Dictionary<BoardRegion, int> doors = new Dictionary<BoardRegion, int>();

            foreach (Vector2Int cell in layout.Cells)
            {
                foreach (Vector2Int d in BoardLayout.Directions)
                {
                    Vector2Int next = cell + d;

                    if (!layout.Contains(next))
                    {
                        Assert.IsTrue(layout.HasWall(cell, d), $"open border at {cell} {d} seed {seed}");
                        continue;
                    }

                    BoardRegion a = layout.RegionOf(cell);
                    BoardRegion b = layout.RegionOf(next);

                    if (a == BoardRegion.Center && b != BoardRegion.Center && layout.CanMove(cell, d))
                        doors[b] = doors.TryGetValue(b, out int n) ? n + 1 : 1;

                    if (a != BoardRegion.Center && b != BoardRegion.Center && a != b)
                        Assert.IsFalse(layout.CanMove(cell, d), "rooms must not connect to each other");
                }
            }

            foreach (BoardRegion r in new[] { BoardRegion.North, BoardRegion.South, BoardRegion.East, BoardRegion.West })
                Assert.AreEqual(1, doors[r], $"{r} seed {seed}");
        }
    }

    [Test]
    public void RoomEntranceLinesUpWithCentralExit()
    {
        BoardLayout layout = BoardLayout.Generate(Settings(), 3);
        Assert.AreEqual(new Vector2Int(5, 11), layout.Entrances[BoardRegion.North]);
        Assert.AreEqual(new Vector2Int(5, -1), layout.Entrances[BoardRegion.South]);
        Assert.AreEqual(new Vector2Int(11, 5), layout.Entrances[BoardRegion.East]);
        Assert.AreEqual(new Vector2Int(-1, 5), layout.Entrances[BoardRegion.West]);
        Assert.IsTrue(layout.CanMove(new Vector2Int(5, 10), Vector2Int.up));
    }
}

public class DuelTests
{
    [Test]
    public void HigherResultWinsAndLoserTakesDamage()
    {
        var attacker = new CombatContext(0, 0, 1) { Roll = 5 };
        var defender = new CombatContext(1, 0, 1) { Roll = 3 };
        DuelOutcome outcome = DuelResolver.Resolve(attacker, defender);
        Assert.AreEqual(1, outcome.Winner);
        Assert.AreEqual(0, outcome.AttackerLifeLoss);
        Assert.AreEqual(1, outcome.DefenderLifeLoss);
    }

    [Test]
    public void CardsChangeTheDuel()
    {
        var attacker = new CombatContext(0, 0, 1) { Roll = 3 };
        var defender = new CombatContext(1, 0, 1) { Roll = 4 };
        attacker.RollModifier += 2;
        DuelOutcome outcome = DuelResolver.Resolve(attacker, defender);
        Assert.AreEqual(1, outcome.Winner);
    }

    [Test]
    public void DefenceCardProtectsLoser()
    {
        var attacker = new CombatContext(0, 0, 1) { Roll = 2 };
        var defender = new CombatContext(1, 0, 1) { Roll = 6, };
        attacker.DamageReduction = 1;
        DuelOutcome outcome = DuelResolver.Resolve(attacker, defender);
        Assert.AreEqual(-1, outcome.Winner);
        Assert.AreEqual(0, outcome.AttackerLifeLoss);
    }

    [Test]
    public void DrawHurtsNobody()
    {
        var attacker = new CombatContext(0, 0, 1) { Roll = 4 };
        var defender = new CombatContext(1, 0, 1) { Roll = 4 };
        DuelOutcome outcome = DuelResolver.Resolve(attacker, defender);
        Assert.AreEqual(0, outcome.Winner);
        Assert.AreEqual(0, outcome.AttackerLifeLoss + outcome.DefenderLifeLoss);
    }
}

public class RouletteTests
{
    private static List<RouletteOption> Options(params float[] weights)
    {
        var list = new List<RouletteOption>();
        foreach (float w in weights) list.Add(new RouletteOption { weight = w });
        return list;
    }

    [Test]
    public void PickFollowsCumulativeWeights()
    {
        var options = Options(1f, 2f, 1f); // [0,0.25) [0.25,0.75) [0.75,1)
        Assert.AreEqual(0, RouletteConfig.Pick(options, 0.0));
        Assert.AreEqual(0, RouletteConfig.Pick(options, 0.24));
        Assert.AreEqual(1, RouletteConfig.Pick(options, 0.25));
        Assert.AreEqual(1, RouletteConfig.Pick(options, 0.74));
        Assert.AreEqual(2, RouletteConfig.Pick(options, 0.75));
        Assert.AreEqual(2, RouletteConfig.Pick(options, 0.9999));
    }

    [Test]
    public void ZeroWeightNeverWins()
    {
        var options = Options(0f, 1f, 0f);
        for (int i = 0; i < 100; i++)
            Assert.AreEqual(1, RouletteConfig.Pick(options, i / 100.0));

        Assert.AreEqual(-1, RouletteConfig.Pick(Options(0f, 0f), 0.5));
    }

    [Test]
    public void DistributionMatchesWeights()
    {
        var options = RouletteConfig.CreateDefaultOptions();
        var rng = new System.Random(42);
        int[] hits = new int[options.Count];
        const int samples = 200000;

        for (int i = 0; i < samples; i++)
            hits[RouletteConfig.Pick(options, rng.NextDouble())]++;

        float total = 0f;
        foreach (var o in options) total += o.weight;

        for (int i = 0; i < options.Count; i++)
            Assert.AreEqual(options[i].weight / total, hits[i] / (float)samples, 0.01f, options[i].label);
    }

    [Test]
    public void DefaultsCoverEveryEffectAndLifeLossIsRarest()
    {
        var options = RouletteConfig.CreateDefaultOptions();
        var effects = new HashSet<RouletteEffectType>();
        float lifeLoss = 0f, minOther = float.MaxValue;

        foreach (var o in options)
        {
            effects.Add(o.effect);
            if (o.effect == RouletteEffectType.LoseLife) lifeLoss = o.weight;
            else minOther = Mathf.Min(minOther, o.weight);
        }

        foreach (RouletteEffectType e in System.Enum.GetValues(typeof(RouletteEffectType)))
            Assert.IsTrue(effects.Contains(e), e.ToString());

        Assert.Less(lifeLoss, minOther);
    }
}

public class RoomThemeAssignmentTests
{
    [Test]
    public void ChosenCharactersFillSidesInPlayerOrder()
    {
        int[] sides = RoomThemeAssignment.Assign(new List<int> { 3, 0 }, 6, 123);
        Assert.AreEqual(3, sides[0]); // North = player 1
        Assert.AreEqual(0, sides[1]); // East  = player 2
    }

    [Test]
    public void FreeSidesUseDistinctUnchosenCharacters()
    {
        int[] sides = RoomThemeAssignment.Assign(new List<int> { 3, 0 }, 6, 123);
        CollectionAssert.AllItemsAreUnique(sides);
        CollectionAssert.DoesNotContain(new[] { sides[2], sides[3] }, 3);
        CollectionAssert.DoesNotContain(new[] { sides[2], sides[3] }, 0);
    }

    [Test]
    public void SameSeedSameRooms()
    {
        CollectionAssert.AreEqual(
            RoomThemeAssignment.Assign(new List<int> { 1 }, 6, 77),
            RoomThemeAssignment.Assign(new List<int> { 1 }, 6, 77));
    }

    [Test]
    public void FourPlayersUseOnlyTheirCharactersAndInvalidIdsAreSkipped()
    {
        CollectionAssert.AreEqual(new[] { 5, 4, 2, 1 }, RoomThemeAssignment.Assign(new List<int> { 5, 4, 2, 1 }, 6, 1));

        int[] sides = RoomThemeAssignment.Assign(new List<int> { -1, 2, 2 }, 6, 1);
        Assert.AreEqual(2, sides[0]);
        CollectionAssert.AllItemsAreUnique(sides);
    }
}

public class SpecialCardTests
{
    private static CardData Card(SpecialAbility? ability)
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        if (ability.HasValue)
        {
            var effect = ScriptableObject.CreateInstance<SpecialCardEffect>();
            effect.ability = ability.Value;
            card.effect = effect;
        }
        return card;
    }

    [Test]
    public void TimingPerAbility()
    {
        Assert.IsTrue(CardRules.CanPlayOnOwnTurn(Card(SpecialAbility.AvoidEvents)));
        Assert.IsTrue(CardRules.CanPlayOnOwnTurn(Card(SpecialAbility.WallWalk)));
        Assert.IsTrue(CardRules.CanPlayOnOwnTurn(Card(SpecialAbility.Heal)));

        Assert.IsTrue(CardRules.CanPlayInDuel(Card(SpecialAbility.EscapeDuel)));
        Assert.IsFalse(CardRules.CanPlayInEventCombat(Card(SpecialAbility.EscapeDuel)));

        Assert.IsTrue(CardRules.CanPlayInEventCombat(Card(SpecialAbility.EscapeEvent)));
        Assert.IsFalse(CardRules.CanPlayInDuel(Card(SpecialAbility.EscapeEvent)));

        var revive = Card(SpecialAbility.Revive);
        Assert.IsFalse(CardRules.CanPlayOnOwnTurn(revive));
        Assert.IsFalse(CardRules.CanPlayInDuel(revive));
        Assert.IsFalse(CardRules.CanPlayInEventCombat(revive));
    }

    [Test]
    public void NormalCardsOnlyInCombat()
    {
        var plain = Card(null);
        Assert.IsTrue(CardRules.CanPlayInEventCombat(plain));
        Assert.IsTrue(CardRules.CanPlayInDuel(plain));
        Assert.IsFalse(CardRules.CanPlayOnOwnTurn(plain));
    }

    [Test]
    public void ChanceRoll()
    {
        Assert.IsTrue(SpecialCardEffect.Succeeds(0.65f, 0.64));
        Assert.IsFalse(SpecialCardEffect.Succeeds(0.65f, 0.65));
        Assert.IsTrue(SpecialCardEffect.Succeeds(0.85f, 0.0));
        Assert.IsFalse(SpecialCardEffect.Succeeds(0f, 0.0));
    }

    [Test]
    public void SpecialCardsDoNotChangeTheRoll()
    {
        var ctx = new CombatContext(0, 4, 1) { Roll = 3 };
        Card(SpecialAbility.EscapeEvent).Apply(ctx);
        Assert.AreEqual(3, ctx.Result);
        Assert.AreEqual(4, ctx.Required);
    }
}
