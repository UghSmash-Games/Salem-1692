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
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI headerlabel; // Ensure this is linked in Inspector

         private readonly List<Button> spawned = new();
        private Player source;
        private Player primary;
        private Player secondary;
        
        // Mode Flags
        private bool requireTwo;
        private bool isSingleTargetMode;
        private bool isAttackMode;
        
        // Callbacks
        private Action<Player, Player> onDone; // Legacy 2-target callback
        private Action<Player, bool> onSingleTargetConfirm; // New 1-target callback

        private bool isOpen;
        private List<Player> candidateOverride;
        private bool useOverride;

        // -----------------------------------------------------------------------------------
        // OPEN METHOD (Supports Black Cat & Night Actions)
        // -----------------------------------------------------------------------------------
        public void Open(Player source, bool isAttack, Action<Player, bool> onConfirm, List<Player> validTargets, bool isSingleTarget = true, string promptOverride = null)
        {
            this.source = source;
            this.isAttackMode = isAttack;
            this.onSingleTargetConfirm = onConfirm;

            this.candidateOverride = validTargets;
            this.useOverride = (validTargets != null && validTargets.Count > 0);

            this.isSingleTargetMode = isSingleTarget;
            this.requireTwo = !isSingleTarget; 

            isOpen = true;
            gameObject.SetActive(true);

            // Reset Selection
            primary = null;
            secondary = null;

            // Handle Text
            if (headerlabel != null)
            {
                if (!string.IsNullOrEmpty(promptOverride))
                {
                    headerlabel.text = promptOverride;
                }
                else
                {
                    headerlabel.text = isAttack ? "Choose a Target to Attack" : "Choose a Target to Save";
                }
            }

            // Build List (Allow source selection unless specifically excluded in logic)
            BuildList(new HashSet<Player>());

            // Setup Button
            if (confirmButton)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(Confirm);
                confirmButton.interactable = false;
            }
        }

        // -----------------------------------------------------------------------------------
        // LEGACY OPEN METHOD (Keep if other scripts call this specific signature)
        // -----------------------------------------------------------------------------------
        public void OpenLegacy(Player sourcePlayer, bool twoTargets, Action<Player, Player> done, 
                               IEnumerable<Player> candidateOverride = null, bool allowSelfSelection = false, string promptOverride = null)
        {
            this.source = sourcePlayer;
            this.requireTwo = twoTargets;
            this.isSingleTargetMode = !twoTargets;
            this.onDone = done;
            
            // Logic to convert IEnumerable to List
            this.candidateOverride = candidateOverride?.Where(p => p != null && !p.IsEliminated).Distinct().ToList();
            this.useOverride = this.candidateOverride != null && this.candidateOverride.Any();

            isOpen = true;
            gameObject.SetActive(true);
            
            primary = null;
            secondary = null;

            if (headerlabel) headerlabel.text = string.IsNullOrEmpty(promptOverride) ? "Choose Target" : promptOverride;

            BuildList(allowSelfSelection ? new HashSet<Player>() : new HashSet<Player> { source });
            
            if (confirmButton)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(Confirm);
                confirmButton.interactable = false;
            }
        }

        private void BuildList(HashSet<Player> exclude)
        {
            // Clear old
            foreach (var b in spawned) if (b) b.onClick.RemoveAllListeners();
            spawned.Clear();
            foreach (Transform c in listParent) Destroy(c.gameObject);

            // Determine candidates
            var candidates = useOverride
                ? candidateOverride.Where(p => !exclude.Contains(p)).ToList()
                : PlayerService.GetAlivePlayers().Where(p => !exclude.Contains(p)).ToList();

            // Spawn
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

            if (isSingleTargetMode)
            {
                // Single Target Logic: Clicking just selects that player
                primary = p;
                secondary = null;
            }
            else
            {
                // Dual Target Logic
                if (primary == null)
                {
                    primary = p;
                    // If we need a second target, rebuild list to exclude first pick
                    if (requireTwo) BuildList(new HashSet<Player> { source, primary });
                }
                else if (requireTwo && secondary == null && p != primary)
                {
                    secondary = p;
                }
            }

            if (confirmButton)
            {
                // Valid if Primary exists. If Dual mode, Secondary must also exist.
                bool complete = primary != null && (isSingleTargetMode || secondary != null);
                confirmButton.interactable = complete;
            }
        }

        private void Confirm()
        {
            if (!isOpen) return;
            isOpen = false;

            if (isSingleTargetMode)
            {
                onSingleTargetConfirm?.Invoke(primary, isAttackMode);
            }
            else
            {
                onDone?.Invoke(primary, secondary);
            }

            Cleanup();
            gameObject.SetActive(false);
        }

        private void OnDisable() => Cleanup();

        private void Cleanup()
        {
            foreach (var b in spawned) if (b) b.onClick.RemoveAllListeners();
            spawned.Clear();
            primary = secondary = null;
            candidateOverride = null;
            useOverride = false;
            // prompt reset handled in Open
        }
    }
}