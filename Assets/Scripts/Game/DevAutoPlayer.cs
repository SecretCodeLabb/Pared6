#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Development-only bot for end-to-end testing without people.
/// Command line (dev build):
///   -autohost            create the game, START when -players are ready, restart for -games games
///   -autojoin 127.0.0.1  join as client
///   -autoplay            play own turns (see HandleGame)
///   -players N           players needed before the host presses START (default 2)
///   -games N             full games the host plays before leaving (default 1)
///   -quitonmenu          quit when back in the menu after having played
///   -hardrules           (host, testing) harder combats and 2 lives to force eliminations / revives
///   -character NAME      pick this character (CharacterData.characterName) if it is free
///   -demohand            open / close the cards of the hand every few seconds (UI showcase)
/// In the Editor: DevAutoPlayer.Enable(autoPlay: true).
/// It only uses the same requests as the UI; the Host validates everything.
/// </summary>
public class DevAutoPlayer : MonoBehaviour
{
    private const float ActionInterval = 0.5f;
    private const float GameOverWait = 5f;

    private string autoJoinIp;
    private bool autoHost;
    private bool autoPlay;
    private bool quitOnMenu;
    private int playersNeeded = 2;
    private int gamesToPlay = 1;
    private string preferredCharacter;
    private static bool demoHand;
    private float nextHandDemo;
    private int handDemoIndex = -1;

    private bool hasJoined;
    private bool hostStarted;
    private int gamesFinished;
    private float gameOverAt = -1f;
    private float nextAction;
    private readonly System.Random rng = new System.Random();

    public static DevAutoPlayer Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        string ip = null;
        string character = null;
        bool host = false, play = false, quit = false, hard = false;
        int players = 2, games = 1;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-autojoin" && i + 1 < args.Length) ip = args[i + 1];
            if (args[i] == "-autohost") host = true;
            if (args[i] == "-autoplay") play = true;
            if (args[i] == "-quitonmenu") quit = true;
            if (args[i] == "-hardrules") hard = true;
            if (args[i] == "-character" && i + 1 < args.Length) character = args[i + 1];
            if (args[i] == "-demohand") demoHand = true;
            if (args[i] == "-players" && i + 1 < args.Length) int.TryParse(args[i + 1], out players);
            if (args[i] == "-games" && i + 1 < args.Length) int.TryParse(args[i + 1], out games);
        }

        if (ip == null && !host)
            return;

        if (hard)
        {
            // Runtime only (this process); the GameRules asset is not modified.
            GameRules.Instance.requiredCombatRoll = 5;
            GameRules.Instance.startingLives = 2;
            Debug.Log("[DevAutoPlayer] hard rules: requiredCombatRoll 5, startingLives 2");
        }

        DevAutoPlayer bot = Enable(play);
        bot.autoJoinIp = ip;
        bot.autoHost = host;
        bot.quitOnMenu = quit;
        bot.playersNeeded = Mathf.Max(1, players);
        bot.gamesToPlay = Mathf.Max(1, games);
        bot.preferredCharacter = character;
        Debug.Log($"[DevAutoPlayer] host {host} join {ip} play {play} players {bot.playersNeeded} games {bot.gamesToPlay}");
    }

    public static DevAutoPlayer Enable(bool autoPlay)
    {
        if (Active == null)
        {
            GameObject go = new GameObject("DevAutoPlayer");
            DontDestroyOnLoad(go);
            Active = go.AddComponent<DevAutoPlayer>();
        }

        Active.autoPlay = autoPlay;
        return Active;
    }

    public static void Disable()
    {
        if (Active != null)
            Destroy(Active.gameObject);
    }

    private void OnDestroy()
    {
        if (Active == this)
            Active = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextAction)
            return;

        nextAction = Time.unscaledTime + ActionInterval;

        string scene = SceneManager.GetActiveScene().name;
        bool listening = NetworkGameMode.IsActive;

        if (scene == SceneNames.Menu)
        {
            HandleMenu(listening);
            return;
        }

        if (!listening)
            return;

        hasJoined = true;

        if (scene == SceneNames.CharacterSelection)
            HandleLobby();
        else if (scene == SceneNames.Game)
            HandleGame();
    }

    // =========================================================
    // MENU / LOBBY
    // =========================================================

    private void HandleMenu(bool listening)
    {
        if (hasJoined && quitOnMenu)
        {
            Debug.Log("[DevAutoPlayer] back in menu, quitting.");
            Application.Quit();
            return;
        }

        NetworkConnectionManager connection = NetworkConnectionManager.Instance;

        if (hasJoined || listening || connection == null || connection.IsConnecting)
            return;

        if (autoHost && !hostStarted)
        {
            hostStarted = true;
            Debug.Log("[DevAutoPlayer] creating game");
            connection.StartHost();
        }
        else if (autoJoinIp != null)
        {
            Debug.Log("[DevAutoPlayer] joining " + autoJoinIp);
            connection.StartClient(autoJoinIp, GameRules.Instance.port);
        }
    }

    private void HandleLobby()
    {
        gameOverAt = -1f;
        LobbyManager lobby = LobbyManager.Instance;

        if (lobby == null || !lobby.TryGetLocalSlot(out LobbySlot local))
            return;

        if (local.CharacterId == CharacterSelectionRules.NoCharacter)
        {
            HashSet<int> taken = new HashSet<int>();

            foreach (LobbySlot slot in lobby.Slots)
                taken.Add(slot.CharacterId);

            // Random free character so different rooms / missions get tested.
            List<int> free = new List<int>();

            for (int id = 0; id < CharacterCatalog.Instance.Count; id++)
            {
                if (!taken.Contains(id))
                    free.Add(id);
            }

            int preferred = free.FindIndex(id => CharacterCatalog.Instance.Get(id) != null &&
                string.Equals(CharacterCatalog.Instance.Get(id).characterName, preferredCharacter, StringComparison.OrdinalIgnoreCase));

            if (preferred >= 0)
                lobby.RequestSelectCharacterRpc(free[preferred]);
            else if (free.Count > 0)
                lobby.RequestSelectCharacterRpc(free[rng.Next(free.Count)]);

            return;
        }

        if (!local.Ready)
        {
            lobby.RequestReadyRpc(true);
            return;
        }

        if (autoHost && NetworkManager.Singleton.IsHost &&
            lobby.ConnectedCount >= playersNeeded && lobby.CanStartGame())
        {
            Debug.Log($"[DevAutoPlayer] START game {gamesFinished + 1}");
            lobby.ServerStartGame();
        }
    }

    // =========================================================
    // GAME
    // =========================================================

    private void HandleGame()
    {
        TurnManager turns = TurnManager.Instance;
        PlayerNetworkData local = PlayerNetworkData.Local;

        if (turns == null || local == null || !turns.IsSpawned)
            return;

        if (demoHand && Time.unscaledTime >= nextHandDemo && local.Hand.Count > 0)
        {
            // Cycles: closed, card 0 open, closed, card 1 open...
            nextHandDemo = Time.unscaledTime + 2.5f;
            handDemoIndex = handDemoIndex >= 0 ? -1 : (int)(Time.unscaledTime / 5f) % local.Hand.Count;
            GameHUD hud = FindFirstObjectByType<GameHUD>();

            if (hud != null)
                hud.SelectHandCard(handDemoIndex);
        }

        if (turns.State.Value == TurnState.GameOver)
        {
            HandleGameOver();
            return;
        }

        if (!autoPlay)
            return;

        if (turns.CanLocalPlayerRoll())
        {
            // A played card changes the state on the Host; roll on a later tick.
            if (!TryTurnCard(turns, local))
                turns.RequestRollFromLocalPlayer();

            return;
        }

        if (turns.CanLocalPlayerMove())
        {
            Move(turns, local);
            return;
        }

        MissionManager mission = MissionManager.Instance;

        if (mission != null && mission.IsSpawned && mission.Phase.Value == MissionPhase.Active && mission.IsLocalMission)
        {
            // Real minigames that can play themselves are played (and report their own result).
            MissionMinigame minigame = FindFirstObjectByType<MissionMinigame>();

            if (minigame != null && minigame.SupportsAutoPlay)
            {
                if (!minigame.AutoPlay)
                {
                    minigame.AutoPlay = true;
                    Debug.Log("[DevAutoPlayer] mission minigame autoplay: " + minigame.GetType().Name);
                }

                return;
            }

            // Fail sometimes so the retry path gets exercised.
            bool success = rng.NextDouble() < 0.6;
            Debug.Log("[DevAutoPlayer] mission " + (success ? "completed" : "failed"));
            mission.RequestFinishFromLocal(success);
            return;
        }

        RouletteManager roulette = RouletteManager.Instance;

        if (roulette != null && roulette.CanLocalSpin())
        {
            roulette.RequestSpinFromLocal();
            return;
        }

        HandleCombat(local);
    }

    private void HandleGameOver()
    {
        if (gameOverAt < 0f)
        {
            gameOverAt = Time.unscaledTime;
            GameStateManager game = GameStateManager.Instance;
            PlayerNetworkData winner = PlayerNetworkData.GetByPlayerId(game.WinnerPlayerId.Value);
            Debug.Log($"[DevAutoPlayer] GAME OVER. Winner: {(winner != null ? winner.Name + " mission " + winner.MissionCompleted.Value : "none")}");
            return;
        }

        if (!autoHost || !NetworkManager.Singleton.IsHost || Time.unscaledTime - gameOverAt < GameOverWait)
            return;

        gamesFinished++;
        gameOverAt = float.MaxValue;

        if (gamesFinished < gamesToPlay)
        {
            Debug.Log("[DevAutoPlayer] restarting");
            GameStateManager.Instance.ServerRestart();
        }
        else
        {
            Debug.Log("[DevAutoPlayer] all games played, leaving");
            NetworkConnectionManager.Instance.ReturnToMenu("");
        }
    }

    /// <summary>Plays an own-turn special card now and then (heal only when hurt).</summary>
    private bool TryTurnCard(TurnManager turns, PlayerNetworkData local)
    {
        if (rng.NextDouble() > 0.4)
            return false;

        for (int i = 0; i < local.Hand.Count; i++)
        {
            CardData card = CardDatabase.Instance.GetById(local.Hand[i]);

            if (!CardRules.CanPlayOnOwnTurn(card) || !turns.CanLocalPlayTurnCard(i))
                continue;

            if (card.effect is SpecialCardEffect special && special.ability == SpecialAbility.Heal &&
                local.Lives.Value >= GameRules.Instance.startingLives)
                continue;

            Debug.Log("[DevAutoPlayer] plays turn card " + card.displayName);
            turns.RequestPlayTurnCardFromLocal(i);
            return true;
        }

        return false;
    }

    /// <summary>Usually walks towards the own mission tile (shortest path), sometimes wanders.</summary>
    private void Move(TurnManager turns, PlayerNetworkData local)
    {
        PlayerController controller = local.GetComponent<PlayerController>();

        if (controller != null && controller.IsAnimating)
            return;

        BoardManager board = GameStateManager.Instance.Board;
        Tile tile = board.GetTile(local.GridPosition.Value);
        Vector2Int direction = Vector2Int.zero;

        if (rng.NextDouble() < 0.75 && GameStateManager.Instance.TryGetMissionCell(local, out Vector2Int target))
            direction = FirstStepTowards(board, tile, target);

        if (direction == Vector2Int.zero)
        {
            List<Vector2Int> options = new List<Vector2Int>();

            foreach (Vector2Int d in BoardLayout.Directions)
            {
                if (board.GetNeighbor(tile, d) != null)
                    options.Add(d);
            }

            if (options.Count > 0)
                direction = options[rng.Next(options.Count)];
        }

        if (direction != Vector2Int.zero)
            turns.RequestMoveFromLocalPlayer(direction);
    }

    private static Vector2Int FirstStepTowards(BoardManager board, Tile start, Vector2Int target)
    {
        if (start == null || start.gridPosition == target)
            return Vector2Int.zero;

        Dictionary<Vector2Int, Vector2Int> firstStep = new Dictionary<Vector2Int, Vector2Int>();
        Queue<Tile> queue = new Queue<Tile>();
        HashSet<Vector2Int> seen = new HashSet<Vector2Int> { start.gridPosition };
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Tile current = queue.Dequeue();

            foreach (Vector2Int d in BoardLayout.Directions)
            {
                Tile next = board.GetNeighbor(current, d);

                if (next == null || !seen.Add(next.gridPosition))
                    continue;

                firstStep[next.gridPosition] = current == start ? d : firstStep[current.gridPosition];

                if (next.gridPosition == target)
                    return firstStep[next.gridPosition];

                queue.Enqueue(next);
            }
        }

        return Vector2Int.zero;
    }

    private void HandleCombat(PlayerNetworkData local)
    {
        CombatManager combat = CombatManager.Instance;

        if (combat == null || !combat.IsLocalFighter)
            return;

        if (combat.CanLocalUseCard() && rng.NextDouble() < 0.5)
        {
            for (int i = 0; i < local.Hand.Count; i++)
            {
                CardData card = CardDatabase.Instance.GetById(local.Hand[i]);
                bool playable = combat.IsDuel.Value ? CardRules.CanPlayInDuel(card) : CardRules.CanPlayInEventCombat(card);

                if (playable)
                {
                    Debug.Log("[DevAutoPlayer] plays combat card " + card.displayName);
                    combat.RequestUseCardFromLocal(i, local.Hand[i]);
                    return;
                }
            }
        }

        if (combat.CanLocalRoll())
            combat.RequestRollFromLocal();
    }
}
#endif
