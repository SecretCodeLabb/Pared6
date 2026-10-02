#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Development-only bot for end-to-end testing without a second person.
/// Command line (dev build): -autojoin 127.0.0.1  [-autoplay]  [-quitonmenu]
///   -autojoin   connects as client, picks the first free character and presses LISTO.
///   -autoplay   also plays its own turns (roll, random valid steps, combat).
///   -quitonmenu quits the app when it lands back in the menu after having joined.
/// In the Editor it can be enabled from code: DevAutoPlayer.Enable(autoPlay: true).
/// Uses exactly the same requests as the UI; the Host validates everything.
/// </summary>
public class DevAutoPlayer : MonoBehaviour
{
    private const float ActionInterval = 0.5f;

    private string autoJoinIp;
    private bool autoPlay;
    private bool quitOnMenu;
    private bool hasJoined;
    private float nextAction;
    private readonly System.Random rng = new System.Random();

    public static DevAutoPlayer Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        string ip = null;
        bool play = false;
        bool quit = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-autojoin" && i + 1 < args.Length) ip = args[i + 1];
            if (args[i] == "-autoplay") play = true;
            if (args[i] == "-quitonmenu") quit = true;
        }

        if (ip == null)
            return;

        DevAutoPlayer bot = Enable(play);
        bot.autoJoinIp = ip;
        bot.quitOnMenu = quit;
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
        Debug.Log("[DevAutoPlayer] enabled (autoPlay " + autoPlay + ")");
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
        else if (scene == SceneNames.Game && autoPlay)
            HandleGame();
    }

    private void HandleMenu(bool listening)
    {
        if (hasJoined && quitOnMenu)
        {
            Debug.Log("[DevAutoPlayer] back in menu, quitting.");
            Application.Quit();
            return;
        }

        if (!hasJoined && !listening && autoJoinIp != null && NetworkConnectionManager.Instance != null &&
            !NetworkConnectionManager.Instance.IsConnecting)
        {
            Debug.Log("[DevAutoPlayer] joining " + autoJoinIp);
            NetworkConnectionManager.Instance.StartClient(autoJoinIp, GameRules.Instance.port);
        }
    }

    private void HandleLobby()
    {
        LobbyManager lobby = LobbyManager.Instance;

        if (lobby == null || !lobby.TryGetLocalSlot(out LobbySlot local))
            return;

        if (local.CharacterId == CharacterSelectionRules.NoCharacter)
        {
            HashSet<int> taken = new HashSet<int>();

            foreach (LobbySlot slot in lobby.Slots)
                taken.Add(slot.CharacterId);

            for (int id = 0; id < CharacterCatalog.Instance.Count; id++)
            {
                if (!taken.Contains(id))
                {
                    lobby.RequestSelectCharacterRpc(id);
                    return;
                }
            }

            return;
        }

        if (!local.Ready)
            lobby.RequestReadyRpc(true);
    }

    private void HandleGame()
    {
        TurnManager turns = TurnManager.Instance;
        CombatManager combat = CombatManager.Instance;
        PlayerNetworkData local = PlayerNetworkData.Local;

        if (turns == null || local == null || !turns.IsSpawned)
            return;

        if (turns.CanLocalPlayerRoll())
        {
            turns.RequestRollFromLocalPlayer();
            return;
        }

        if (turns.CanLocalPlayerMove())
        {
            PlayerController controller = local.GetComponent<PlayerController>();

            if (controller != null && controller.IsAnimating)
                return;

            BoardManager board = GameStateManager.Instance.Board;
            Tile tile = board.GetTile(local.GridPosition.Value);
            List<Vector2Int> options = new List<Vector2Int>();

            foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                if (board.GetNeighbor(tile, d) != null)
                    options.Add(d);
            }

            if (options.Count > 0)
                turns.RequestMoveFromLocalPlayer(options[rng.Next(options.Count)]);

            return;
        }

        if (combat == null || !combat.IsLocalFighter)
            return;

        if (combat.CanLocalUseCard() && rng.Next(2) == 0)
        {
            combat.RequestUseCardFromLocal(0, local.Hand[0]);
            return;
        }

        if (combat.CanLocalRoll())
            combat.RequestRollFromLocal();
    }
}
#endif
