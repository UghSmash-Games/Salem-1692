/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Central controller that links gameplay systems, tracks players, and checks endgame conditions.
*   Responsibilities:
*        • Initialize game
*        • Track players
*        • Delegate to phase/turn/state managers
*        • Trigger endgame when conditions met
*   Access Requirements:
*        • GamePhaseManager
*        • GameTurnManager
*        • PlayerManager
*        • DeckManager
*        • UIManager

* TODO: [Planned improvements]
 * FIXME: [Known bugs or issues]
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.Players;
using Salem.UI;
using UnityEngine;

namespace Salem.GameFlow
{
    [DefaultExecutionOrder(-100)] // ensure this Awake Runs before other managers
    public class GameManager : MonoBehaviour
    {
        #region Vars
        [Header("RNG")]
        [SerializeField] private bool useFixedSeed = false;
        [SerializeField] private ulong fixedSeed = 123456789UL;
        public IRng Rng { get; private set; }
        public ulong Seed{ get; private set; }

        //Tracks GameManager
        public static GameManager Instance { get; private set; }
        [SerializeField] private UIManager UIManager;

        public event Action<EndGameResult> OnGameEnded;

        //for central control of input/time when game ends
        [SerializeField] private bool pauseOnGameEnd = true;


        private bool isGameActive;
        private bool gameAlreadyEnded = false;
        #endregion

        #region Standard Functions
        private void OnValidate()
        {
            if (!UIManager) UIManager = FindFirstObjectByType<UIManager>();
        }
        void Awake()
        {
            HandleInstance();
            // Local mode discovers pre-placed tagged players. In networked mode the
            // NetworkGameCoordinator instantiates and registers players on join.
            if (PlayerService.Mode == GameMode.Local)
            {
                PopulatePlayers();
            }
            isGameActive = true;
        }

        void Start()
        {
            //Debug.Log($"[GameManager] Total players registered: {PlayerService.All.Count}");
            /*foreach (var p in PlayerService.All)
            {
                Debug.Log($" - Player: {p.PlayerNameText}, IsLocal: {p.IsLocalPlayer}");
            }
            */
            // Networked mode has no single local player (every human is remote).
            var localPlayer = PlayerService.GetLocalPlayer();
            if (localPlayer != null)
            {
                UIManager.SetupLocalPlayerUI(localPlayer);
            }

            /*AIR CONSOLE DISABLED 4/28/26
            // In AirConsole mode, there is no single local player — input comes from phones
            if (!PlayerService.IsAirConsoleMode)
            {
                UIManager.SetupLocalPlayerUI(PlayerService.GetLocalPlayer());
            }
            */
        }
        #endregion

        #region Accessor Functions
        /// <summary>
        /// The ONE authority for "witches win": every alive player is a witch (≥1). This single
        /// condition covers BOTH rulebook triggers — "witches eliminate all townspeople" AND "the
        /// final remaining townsperson becomes a witch" (that player now holds a witch tryal → IsWitch
        /// → not a non-witch). SALEM HAS NO PARITY WIN CONDITION — `witches >= nonWitches` was a
        /// Werewolf/Mafia import and is NOT in the rulebook. Never reintroduce it. Used by both
        /// EvaluateEndGame and CascadeWouldEndBothTeams so the definition can't drift.
        /// </summary>
        private static bool WitchesControl(IReadOnlyCollection<Player> aliveSet) =>
            aliveSet != null && aliveSet.Count > 0 && aliveSet.All(p => p.IsWitch);

        /// <param name="justTurnedWitch">
        /// If this evaluation was triggered by the LAST non-witch just becoming a witch (via a
        /// conspiracy swap — AddTryalCardAndNotify passes itself), that player LOSES per the rulebook
        /// ("in which case that player loses") and is excluded from the winning witch team. Null for
        /// every other trigger (eliminations, reveals) — those credit all alive witches.
        /// </param>
        public void EvaluateEndGame(Player justTurnedWitch = null)
        {
            var alive = PlayerService.GetAlivePlayers();
            if (alive == null || alive.Count == 0) return;

             // Townspeople win when ALL Witch Tryal cards in the game have been revealed.
            // Check across all players (alive and eliminated) for any unrevealed Witch cards.
            // Checked FIRST so a reveal-win beats any elimination count (ties go to townspeople).
            bool anyUnrevealedWitch = PlayerService.All.Any(p =>
                p.TryalCards != null && p.TryalCards.Any(c =>
                    c.TryalCardType == TryalCardType.Witch && !c.IsRevealed));

            if (!anyUnrevealedWitch)
            {
                var winners = alive.Where(p => !p.IsWitch).ToList();
                RaiseGameEnded(new EndGameResult(Team.Villagers, winners, "All Witch Tryal cards revealed"));
                return;
            }

            int witches = alive.Count(p => p.IsWitch);

            // Villagers also win if no witches remain alive (redundant with the reveal check above —
            // an eliminated witch has all tryals revealed — but harmless and defensive).
            if (witches == 0)
            {
                var winners = alive.Where(p => !p.IsWitch).ToList();
                RaiseGameEnded(new EndGameResult(Team.Villagers, winners, "All witches eliminated"));
                return;
            }

            // Witches win iff every alive player is a witch (NO parity — see WitchesControl).
            if (WitchesControl(alive))
            {
                // "...in which case that player loses": exclude the last townsperson who JUST turned.
                var winners = alive.Where(p => p.IsWitch && p != justTurnedWitch).ToList();
                RaiseGameEnded(new EndGameResult(Team.Witches, winners, "All townspeople eliminated or turned"));
                return;
            }
        }

        /// <summary>
        /// #7 both-teams-lose rule (GENERAL — any matchmaker cascade). Non-mutating hypothetical: would
        /// eliminating the matched <paramref name="partner"/> (on top of the already-eliminated
        /// <paramref name="intendedTarget"/>) satisfy BOTH win conditions at once? If so the cascade must
        /// spare the partner and only the intended target dies. Win logic stays centralized here; the
        /// cascade in PlayerService.Eliminate just reads this. Reads only — mutates nothing.
        /// </summary>
        public bool CascadeWouldEndBothTeams(Player intendedTarget, Player partner)
        {
            if (partner == null) return false;

            // Villagers win: no unrevealed Witch tryals remain, treating BOTH the intended target's and
            // the partner's tryals as revealed (elimination reveals tryals — the double-kill reveals both).
            bool wouldVillagersWin = !PlayerService.All.Any(p =>
                p != partner && p != intendedTarget && p.TryalCards != null &&
                p.TryalCards.Any(c => c.TryalCardType == TryalCardType.Witch && !c.IsRevealed));

            // Witches win: among alive minus the partner (the intended target is already eliminated →
            // already excluded from GetAlivePlayers()), every remaining player is a witch — i.e. the
            // partner was the last non-witch. NO parity (see WitchesControl). Reachable only when the
            // survivors are "sticky lost-card witches" (IsWitch true, witch tryal already revealed) —
            // otherwise wouldVillagersWin can't also hold. Same reachability as before; correct condition.
            var aliveAfter = PlayerService.GetAlivePlayers().Where(p => p != partner).ToList();
            bool wouldWitchesWin = WitchesControl(aliveAfter);

            return wouldVillagersWin && wouldWitchesWin;
        }

        // Call EvaluateEndGame() at key points:
        public void OnDayLynchResolved() => EvaluateEndGame();
        public void OnNightResolved() => EvaluateEndGame();
        public void OnPlayerLeftGame() => EvaluateEndGame();

        public void InitRng(ulong? seed = null)
        {
            Seed = seed ?? (useFixedSeed ? fixedSeed : (ulong)System.DateTime.UtcNow.Ticks);
            Rng  = new XorShiftRng(Seed);
            //Debug.Log($"[GameManager] RNG initialized Seed={Seed}");
        }

        // Optional for replays/debug:
        public void Reseed(ulong newSeed) => InitRng(newSeed);
        #endregion

        #region Helper Functions
        //Ensures only 1 GameManager
        private void HandleInstance()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitRng(); // set Rng + Seed
        }

        //Finds ALL Players and stores them in an accessible list
        private void PopulatePlayers()
        {
            // Ensure the list is empty before populating
            PlayerService.Clear();

            GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
            GameObject[] aiObjects = GameObject.FindGameObjectsWithTag("AI");

            foreach (var obj in playerObjects.Concat(aiObjects))
            {
                Player playerComponent = obj.GetComponent<Player>();
                if (playerComponent != null)
                {
                    PlayerService.Register(playerComponent);
                }
                else
                {
                    Debug.LogWarning($"GameObject {obj.name} is tagged as Player/AI but has no Player component.");
                }
            }
        }

        // Last raised end result + a simple query, so the synchronized-reveal helper
        // can decide whether to emit game_over AFTER the reveal animation (the win
        // check itself runs BEFORE the reveal, per the reveal-tryal sequence).
        public EndGameResult LastEndResult { get; private set; }
        public bool IsGameOver => gameAlreadyEnded;

        private void RaiseGameEnded(EndGameResult result)
        {
            if (gameAlreadyEnded) return;
            gameAlreadyEnded = true;
            LastEndResult = result;
            if (pauseOnGameEnd) Time.timeScale = 0f;
            OnGameEnded?.Invoke(result);
        }

        private void OnEnable()
        {
            PlayerService.OnPlayerEliminated += HandlePlayerEliminated;
        }
        private void OnDisable()
        {
            PlayerService.OnPlayerEliminated -= HandlePlayerEliminated;
        }
        private void HandlePlayerEliminated(Player p, EliminationCause cause)
        {
            EvaluateEndGame();
        }
        #endregion

        //TEMP FOR TESTING 5/16/26
        [ContextMenu("TEST End Game - Villagers Win")]
        private void TestVillagersWin()
        {
            var winners = PlayerService.GetAlivePlayers()
                .Where(p => !p.IsWitch)
                .ToList();

            RaiseGameEnded(new EndGameResult(
                Team.Villagers,
                winners,
                "Test villagers win"
            ));
        }
    }
}