/**
 * Mirror join screen — a second-room display joins by room code only (no
 * display name; mirrors have no player slot). Reached at /display.
 */

import { useState, type FormEvent } from 'react';
import { connect, joinMirror } from '../socket/socketClient';
import { useGameStore } from '../store/gameStore';

export function MirrorJoinScreen() {
  const joinError = useGameStore((s) => s.session.joinError);

  const [code, setCode] = useState('');
  const codeValid = /^[A-Z]{4}$/.test(code);

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!codeValid) return;
    connect();
    joinMirror({ code });
  };

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-6 bg-ink px-6">
      <div className="flex flex-col items-center gap-1">
        <h1 className="text-3xl font-bold tracking-wide text-parchment">
          Salem 1692
        </h1>
        <p className="text-sm uppercase tracking-[0.3em] text-candle">
          Mirror Display
        </p>
      </div>

      <form onSubmit={handleSubmit} className="flex w-full max-w-xs flex-col gap-4">
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

        {joinError && (
          <p className="text-center text-sm text-ember" role="alert">
            {joinError}
          </p>
        )}

        <button
          type="submit"
          disabled={!codeValid}
          className="rounded-md bg-candle px-4 py-3 text-lg font-semibold text-ink transition-opacity disabled:opacity-40"
        >
          Watch Game
        </button>
      </form>
    </div>
  );
}
