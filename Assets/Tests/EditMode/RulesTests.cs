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
    public void ReachingGoalWins()
    {
        int winner = VictoryRules.ResolveWinner(1, new List<int> { 0, 3 }, new List<PlayerLifeState> { A, A }, 3, 2);
        Assert.AreEqual(1, winner);
    }

    [Test]
    public void NoWinnerBelowGoal()
    {
        int winner = VictoryRules.ResolveWinner(0, new List<int> { 2, 2 }, new List<PlayerLifeState> { A, A }, 3, 2);
        Assert.AreEqual(TurnRotation.NoPlayer, winner);
    }

    [Test]
    public void TieGoesToActivePlayer()
    {
        int winner = VictoryRules.ResolveWinner(1, new List<int> { 3, 3 }, new List<PlayerLifeState> { A, A }, 3, 2);
        Assert.AreEqual(1, winner);
    }

    [Test]
    public void LastPlayerStandingWins()
    {
        int winner = VictoryRules.ResolveWinner(0, new List<int> { 0, 0, 0 }, new List<PlayerLifeState> { E, A, E }, 3, 3);
        Assert.AreEqual(1, winner);
    }

    [Test]
    public void SoloGameDoesNotWinByLastStanding()
    {
        int winner = VictoryRules.ResolveWinner(0, new List<int> { 0 }, new List<PlayerLifeState> { A }, 3, 1);
        Assert.AreEqual(TurnRotation.NoPlayer, winner);
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
