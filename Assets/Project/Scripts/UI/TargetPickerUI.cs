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

namespace Salem.UI
{
    public class TargetPickerUI : MonoBehaviour
    {
        [SerializeField] private Transform listParent;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private Button confirmButton;

        private Player source;
        private Player primary;
        private Player secondary;
        private bool pickTwo;
        private Action<Player, Player> onDone;

        public void Open(Player sourcePlayer, bool twoTargets, Action<Player, Player> done)
        {
            gameObject.SetActive(true);
            source = sourcePlayer;
            pickTwo = twoTargets;
            onDone = done;
            primary = null; secondary = null;
            RebuildList(exclude: new HashSet<Player>{ source });
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(Confirm);
            confirmButton.interactable = false;
        }

        private void RebuildList(HashSet<Player> exclude)
        {
            foreach (Transform c in listParent) Destroy(c.gameObject);
            var candidates = PlayerService.GetAlivePlayers().Where(p => !exclude.Contains(p)).ToList();
            foreach (var p in candidates)
            {
                var b = Instantiate(buttonPrefab, listParent).GetComponent<Button>();
                b.GetComponentInChildren<TMPro.TMP_Text>().text = p.PlayerNameText;
                b.onClick.AddListener(() =>
                {
                    if (primary == null) { primary = p; }
                    else if (pickTwo && secondary == null && p != primary) { secondary = p; }
                    confirmButton.interactable = (primary != null && (!pickTwo || secondary != null));
                });
            }
        }

        private void Confirm()
        {
            onDone?.Invoke(primary, secondary);
            gameObject.SetActive(false);
        }
    }
}