/**
 * Canonical Socket.io event names — the single source of truth for the
 * web client, mirrored from docs/protocol.md and CLAUDE.md.
 *
 * Importing from here (rather than typing string literals at call sites)
 * keeps the client from drifting out of sync with the server contract.
 */

// Client → Server
export const CLIENT_TO_SERVER = {
  JOIN_ROOM: 'join_room',
  JOIN_MIRROR: 'join_mirror',
  PLAYER_ACTION: 'player_action',
  SECRET_PHASE_SUBMIT: 'secret_phase_submit',
  CONFESS: 'confess',
  DECK_REARRANGE_SUBMIT: 'deck_rearrange_submit',
  CARD_PICK_SUBMIT: 'card_pick_submit',
  CONFIRM_SUBMIT: 'confirm_submit',
  TARGET_SUBMIT: 'target_submit',
  TRYAL_PICK_SUBMIT: 'tryal_pick_submit',
} as const;

// Server → Client
export const SERVER_TO_CLIENT = {
  JOINED: 'joined',
  PLAYER_JOINED: 'player_joined',
  PLAYER_LEFT: 'player_left',
  ROOM_CLOSED: 'room_closed',
  GAME_STATE_UPDATE: 'game_state_update',
  PRIVATE_STATE: 'private_state',
  SECRET_PHASE_PROMPT: 'secret_phase_prompt',
  ACTION_REQUEST: 'action_request',
  DECK_REARRANGE_REQUEST: 'deck_rearrange_request',
  CARD_PICK_REQUEST: 'card_pick_request',
  CONFIRM_REQUEST: 'confirm_request',
  TARGET_REQUEST: 'target_request',
  TRYAL_PICK_REQUEST: 'tryal_pick_request',
  PHASE_RESOLVE: 'phase_resolve',
  PUBLIC_REVEAL: 'public_reveal',
  GAME_EVENT: 'game_event',
  ELIMINATION_RESULT: 'elimination_result',
  GAME_OVER: 'game_over',
  ERROR_MSG: 'error_msg',
} as const;
