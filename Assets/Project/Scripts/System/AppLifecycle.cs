using Salem.Data;
using Salem.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Salem.Systems
{
    /// <summary>
    /// Quitting and restarting the host application.
    ///
    /// 🔴 WHY THIS IS NOT A ONE-LINER. `Application.Quit()` alone is nearly right, but a RESTART has
    /// to undo four pieces of state that outlive a scene load, and missing any one of them produces a
    /// second game that looks fine and is quietly broken:
    ///
    ///   1. `Time.timeScale` — `GameManager.pauseOnGameEnd` sets it to 0 when the game ends. Reload
    ///      without restoring it and the new game is frozen solid the moment it deals.
    ///   2. `PlayerService` is a STATIC registry. Scene load destroys the seat objects but not the
    ///      lists that point at them, so the next game would start holding destroyed references.
    ///   3. `TimerSettings` is static and LOCKED at StartGame, so the pace control would stay frozen
    ///      for the rest of the session.
    ///   4. `CharacterAbilityDispatcher` and `NetworkManager` are DontDestroyOnLoad singletons. Their
    ///      coroutines keep running across a scene load — a draft awaiting an answer from a phone
    ///      would survive into the new game, holding seats that no longer exist.
    ///
    /// Restart deliberately drops the socket and takes a NEW room code. The relay's rooms are keyed
    /// to the host socket, so closing it makes every phone receive `room_closed`, discard its stored
    /// seat and return to the join screen — which is exactly the state they should be in for a fresh
    /// game. Keeping the socket would leave the relay holding seats this host has forgotten, and the
    /// phones would silently reclaim seats in a game that no longer exists.
    /// </summary>
    public static class AppLifecycle
    {
        /// <summary>
        /// Close the application.
        ///
        /// ⚠ `Application.Quit()` does NOTHING in the Editor — it is a no-op there, which makes a
        /// quit button look broken to whoever is testing. The Editor branch stops play mode instead,
        /// so the control behaves the same way in both places.
        /// </summary>
        public static void Quit()
        {
            Debug.Log("[AppLifecycle] Quit requested.");

            // Let the socket close politely so phones get `room_closed` instead of waiting out a
            // timeout. NetworkManager also does this in OnApplicationQuit, but the ordering of that
            // callback relative to process teardown is not something to rely on.
            if (NetworkManager.Instance != null)
            {
                Object.Destroy(NetworkManager.Instance.gameObject);
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Start a completely fresh game: reload the current scene with every cross-scene singleton
        /// and static reset first. Everyone rejoins with the NEW room code shown on the lobby screen.
        /// </summary>
        public static void RestartGame()
        {
            // ⚠ VALIDATE BEFORE TEARING ANYTHING DOWN. SceneManager.LoadScene needs the scene to be
            // listed in Build Settings; a scene that is merely open has buildIndex -1 and the load
            // throws. Checking afterwards would leave the host with its socket already closed and no
            // new scene to come up in — a dead window. The message names the fix, because the symptom
            // ("New Game does nothing") says nothing about Build Settings.
            var active = SceneManager.GetActiveScene();
            if (active.buildIndex < 0)
            {
                Debug.LogError($"[AppLifecycle] Cannot restart: the scene '{active.name}' is not in " +
                               "Build Settings (File > Build Profiles > Scene List). Nothing was changed.");
                return;
            }

            Debug.Log("[AppLifecycle] Restarting — resetting cross-scene state before reload.");

            // 1. Unfreeze. pauseOnGameEnd left this at 0.
            Time.timeScale = 1f;

            // 2. Unlock the pace control, which StartGame locked for the duration of the game.
            TimerSettings.ResetForNewGame();

            // 3. Empty the static seat registry before the objects it points at are destroyed.
            PlayerService.Clear();

            // 4. Stop any ability coroutine mid-flight (a draft waiting on a phone) and drop its
            //    queue, which still holds seats from the game that just ended.
            if (CharacterAbilityDispatcher.Instance != null)
            {
                CharacterAbilityDispatcher.Instance.ResetForNewGame();
            }

            // 5. Drop the relay connection. The scene's own NetworkManager becomes the new singleton
            //    on reload and opens a fresh room; every phone meanwhile gets `room_closed` and
            //    returns to the join screen with its stored seat cleared.
            if (NetworkManager.Instance != null)
            {
                Object.Destroy(NetworkManager.Instance.gameObject);
            }

            SceneManager.LoadScene(active.buildIndex);
        }
    }
}
