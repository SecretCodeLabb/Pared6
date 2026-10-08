public enum PlayerLifeState : byte
{
    Alive,
    Eliminated,
    Disconnected
}

/// <summary>Turn state machine (AGENT_CONTEXT §7). Owned by the Host.</summary>
public enum TurnState : byte
{
    WaitingForBoard,
    WaitingForRoll,
    Moving,
    CheckingTile,
    Combat,
    TurnEnd,
    GameOver,
    Roulette,
    Mission
}

public enum RoulettePhase : byte
{
    Hidden,
    WaitingSpin,
    Spinning,
    ShowingResult
}

public enum CombatPhase : byte
{
    None,
    CardPhase,
    Rolling,
    Resolving,
    Won,
    Lost,
    Draw,
    Escaped
}
