using UnityEngine;

public enum SpecialAbility
{
    AvoidEvents,   // Silencio líquido: chance to dodge entity fights for N turns
    WallWalk,      // Cívica: move exactly N tiles ignoring walls
    Heal,          // Comida Frita: +N lives (up to the starting lives)
    EscapeDuel,    // Gatos fotocopiados: chance to escape a duel
    Revive,        // Salchipapa: passive, revive with N lives when reaching 0
    EscapeEvent    // Canicas: chance to escape a fight against an entity
}

/// <summary>When a card can be played.</summary>
public enum CardTiming
{
    Combat,        // card phase of an event combat or a duel (Daño / Defensa / Inútil)
    OwnTurn,       // own turn, before rolling
    EventCombat,   // card phase of an event combat only
    Duel,          // card phase of a duel only
    Passive        // never played; triggers by itself
}

/// <summary>
/// Special (purple) card abilities. Values are editable on the asset; the name, description and
/// image live on the CardData. The turn/combat flow (TurnManager, CombatManager, GameStateManager)
/// reads <see cref="ability"/> and these values.
/// </summary>
[CreateAssetMenu(fileName = "SpecialCardEffect", menuName = "PPOP/Cards/Effects/Special")]
public class SpecialCardEffect : CardEffect
{
    public SpecialAbility ability;

    [Tooltip("Tiles (Cívica), lives (Comida Frita / Salchipapa).")]
    [Min(0)] public int amount = 1;

    [Tooltip("Success chance 0..1 (Silencio líquido, Gatos fotocopiados, Canicas).")]
    [Range(0f, 1f)] public float chance = 0.65f;

    [Tooltip("Turns the effect lasts, counting the current one (Silencio líquido).")]
    [Min(1)] public int turns = 2;

    public CardTiming Timing
    {
        get
        {
            switch (ability)
            {
                case SpecialAbility.AvoidEvents:
                case SpecialAbility.WallWalk:
                case SpecialAbility.Heal:
                    return CardTiming.OwnTurn;
                case SpecialAbility.EscapeDuel:
                    return CardTiming.Duel;
                case SpecialAbility.EscapeEvent:
                    return CardTiming.EventCombat;
                default:
                    return CardTiming.Passive;
            }
        }
    }

    /// <summary>Special abilities do not modify the combat roll.</summary>
    public override void Apply(CombatContext context)
    {
    }

    public override string ShortLabel
    {
        get
        {
            switch (ability)
            {
                case SpecialAbility.AvoidEvents:
                case SpecialAbility.EscapeDuel:
                case SpecialAbility.EscapeEvent:
                    return Mathf.RoundToInt(chance * 100f) + "%";
                case SpecialAbility.WallWalk:
                    return amount.ToString();
                case SpecialAbility.Heal:
                    return "+" + amount + "♥";
                default:
                    return "♥";
            }
        }
    }

    /// <summary>Pure chance check: roll01 in [0,1).</summary>
    public static bool Succeeds(float chance, double roll01)
    {
        return roll01 < chance;
    }
}

/// <summary>Helpers to read a card's timing without caring about its effect type.</summary>
public static class CardRules
{
    public static CardTiming TimingOf(CardData card)
    {
        if (card != null && card.effect is SpecialCardEffect special)
            return special.Timing;

        return CardTiming.Combat;
    }

    public static bool CanPlayInEventCombat(CardData card)
    {
        CardTiming timing = TimingOf(card);
        return timing == CardTiming.Combat || timing == CardTiming.EventCombat;
    }

    public static bool CanPlayInDuel(CardData card)
    {
        CardTiming timing = TimingOf(card);
        return timing == CardTiming.Combat || timing == CardTiming.Duel;
    }

    public static bool CanPlayOnOwnTurn(CardData card) => TimingOf(card) == CardTiming.OwnTurn;

    /// <summary>Short Spanish hint about when a card is played (for UI / rejections).</summary>
    public static string TimingHint(CardData card)
    {
        switch (TimingOf(card))
        {
            case CardTiming.OwnTurn: return "Se usa en tu turno, antes de tirar el dado.";
            case CardTiming.EventCombat: return "Se usa en un combate contra una entidad.";
            case CardTiming.Duel: return "Se usa en un duelo contra otro jugador.";
            case CardTiming.Passive: return "Se activa sola al perder tu última vida.";
            default: return "Se usa en combate.";
        }
    }
}
