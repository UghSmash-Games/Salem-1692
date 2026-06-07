/**
 * Join screen — players enter a 4-letter room code and a display name.
 * Reached at /join. On `joined`, the store updates and App routes onward.
 */

import { useState, type FormEvent } from 'react';
import { connect, joinRoom } from '../socket/socketClient';
import { useGameStore } from '../store/gameStore';

export function JoinScreen() {
  const joinError = useGameStore((s) => s.session.joinError);
  const beginJoin = useGameStore((s) => s.beginJoin);

  const [code, setCode] = useState('');
  const [name, setName] = useState('');

  const codeValid = /^[A-Z]{4}$/.test(code);
  const nameValid = name.trim().length > 0;
  const canSubmit = codeValid && nameValid;

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    const displayName = name.trim();
    beginJoin(displayName);
    connect();
    joinRoom({ code, displayName });
  };

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-6 bg-ink px-6">
      <h1 className="text-3xl font-bold tracking-wide text-parchment">
        Salem 1692
      </h1>

      <form
        onSubmit={handleSubmit}
        className="flex w-full max-w-xs flex-col gap-4"
      >
        <label className="flex flex-col gap-1">
          <span className="text-sm text-parchment/80">Room code</span>
          <input
            value={code}
            onChange={(e) =>
              setCode(
                e.target.value.toUpperCase().replace(/[^A-Z]/g, '').slice(0, 4),
              )
            }
            placeholder="ABCD"
            autoCapitalize="characters"
            autoComplete="off"
            maxLength={4}
            className="rounded-md border border-parchment/40 bg-parchment/10 px-4 py-3 text-center text-2xl uppercase tracking-[0.5em] text-parchment placeholder:text-parchment/30 focus:border-candle focus:outline-none"
          />
        </label>

        <label className="flex flex-col gap-1">
          <span className="text-sm text-parchment/80">Display name</span>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Goody Proctor"
            autoComplete="off"
            maxLength={20}
            className="rounded-md border border-parchment/40 bg-parchment/10 px-4 py-3 text-parchment placeholder:text-parchment/30 focus:border-candle focus:outline-none"
          />
        </label>

        {joinError && (
          <p className="text-center text-sm text-ember" role="alert">
            {joinError}
          </p>
        )}

        <button
          type="submit"
          disabled={!canSubmit}
          className="rounded-md bg-candle px-4 py-3 text-lg font-semibold text-ink transition-opacity disabled:opacity-40"
        >
          Join Game
        </button>
      </form>
    </div>
  );
}
