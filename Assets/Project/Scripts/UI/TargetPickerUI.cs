/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Resolves logic when cards are played.
*   Responsibilities:
*        • Interpret effect type
*        • Trigger gameplay consequences
*   Access Requirements:
*        • DeckManager
*        • Player
*        • GameStateManager

* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Salem.Data;
using Salem.Players;
using TMPro;

namespace Salem.UI
{
    public class TargetPickerUI : MonoBehaviour
    {
        [SerializeField] private Transform listParent;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text promptLabel;

        private readonly List<Button> spawned = new();
        private Player source;
        private Player primary;
        private Player secondary;
        private bool requireTwo;
        private Action<Player, Player> onDone;
        private bool isOpen;
        private List<Player> candidateOverride;
        private bool useOverride;
        private bool allowSourceSelection;
        private string defaultPrompt;

        private void Awake()
        {
            if (promptLabel)
            {
                defaultPrompt = promptLabel.text;
            }
        }

        public void Open(Player sourcePlayer, bool twoTargets, Action<Player, Player> done,
                         IEnumerable<Player> candidateOverride = null,
                         bool allowSelfSelection = false,
                         string promptOverride = null)
        {
            isOpen = true;
            gameObject.SetActive(true);

            source = sourcePlayer;
            requireTwo = twoTargets;
            onDone = done;
            primary = null;
            secondary = null;
            allowSourceSelection = allowSelfSelection;

            candidateOverride = candidateOverride?.Where(p => p != null && !p.IsEliminated)
                                                   .Distinct()
                                                   .ToList();
            this.candidateOverride = candidateOverride != null && candidateOverride.Any()
                ? new List<Player>(candidateOverride)
                : null;
            useOverride = this.candidateOverride != null;

            if (promptLabel)
            {
                promptLabel.text = string.IsNullOrEmpty(promptOverride) ? defaultPrompt : promptOverride;
            }

            BuildList(allowSourceSelection ? new HashSet<Player>()
                                           : new HashSet<Player> { source });
            
            if (!confirmButton)
            {
                Debug.LogError("[TargetPickerUI] ConfirmButton is not assigned.");
            }
            else
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(Confirm);
                confirmButton.interactable = false;
            }
        }

        private void BuildList(HashSet<Player> exclude)
        {
            // remove listeners then destroy old rows
            foreach (var b in spawned)
                if (b) b.onClick.RemoveAllListeners();
            spawned.Clear();

            foreach (Transform c in listParent)
                Destroy(c.gameObject);

            var candidates = useOverride
                ? candidateOverride.Where(p => !exclude.Contains(p)).ToList()
                : PlayerService.GetAlivePlayers().Where(p => !exclude.Contains(p)).ToList();
            foreach (var p in candidates)
            {
                var go = Instantiate(buttonPrefab, listParent);
                var b = go.GetComponent<Button>();
                spawned.Add(b);

                var label = b.GetComponentInChildren<TMP_Text>();
                if (label) label.text = p.PlayerNameText;

                b.onClick.AddListener(() => OnPick(p));
            }
        }

        private void OnPick(Player p)
        {
            if (!isOpen) return;

            if (primary == null)
            {
                primary = p;
                if (requireTwo)
                    BuildList(new HashSet<Player> { source, primary }); // second pick
            }
            else if (requireTwo && secondary == null && p != primary)
            {
                secondary = p;
            }

            if (confirmButton) // guard destroyed refs
                confirmButton.interactable = (primary != null && (!requireTwo || secondary != null));
        }

        private void Confirm()
        {
            if (!isOpen) return;
            isOpen = false;

            onDone?.Invoke(primary, secondary);
            Cleanup();
            gameObject.SetActive(false);
        }

        private void OnDisable() => Cleanup();

        private void Cleanup()
        {
            foreach (var b in spawned)
                if (b) b.onClick.RemoveAllListeners();
            spawned.Clear();
            primary = secondary = null;
            candidateOverride = null;
            useOverride = false;
            allowSourceSelection = false;
            if (promptLabel)
                promptLabel.text = defaultPrompt;
        }
    }
}