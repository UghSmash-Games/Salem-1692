'use strict';

const express = require('express');
const { createServer } = require('http');
const { Server } = require('socket.io');
const cors = require('cors');
const { registerDispatch } = require('./dispatch');

const app = express();
const httpServer = createServer(app);

// The deployed webclient's origin. Getting this wrong does not fail loudly on its own — the server
// runs perfectly and every browser is simply refused — so an unset value in production is called out
// at boot below. The Unity host is unaffected either way: it is a native socket and sends no Origin.
const DEV_ORIGIN = 'http://localhost:5173';
const corsOrigin = process.env.CORS_ORIGIN || DEV_ORIGIN;

app.use(cors({ origin: corsOrigin }));

const io = new Server(httpServer, {
  cors: {
    origin: corsOrigin,
    methods: ['GET', 'POST'],
  },
});

registerDispatch(io);

// Health check
app.get('/health', (_req, res) => {
  res.json({ status: 'ok' });
});

const PORT = process.env.PORT || 3000;

/**
 * Shut down without cutting live sockets mid-frame.
 *
 * ⚠ THIS DOES NOT PRESERVE GAMES. Rooms live in memory, so any restart ends every game in progress —
 * a deploy is a table-flip, not a rolling update. What this buys is a clean close: clients get a
 * proper disconnect (and each phone's stored seat is dropped on `room_closed`) instead of a hung
 * socket that waits for a timeout. Deploy between sessions, not during one.
 */
function shutdown(signal) {
  console.log(`[server] ${signal} received — closing connections.`);
  io.close(() => {
    httpServer.close(() => {
      console.log('[server] closed.');
      process.exit(0);
    });
  });

  // Fly sends SIGKILL after its grace period; exit first so the log line above is not the last
  // thing anyone sees from a process that then hangs.
  setTimeout(() => {
    console.warn('[server] forced exit — connections did not close in time.');
    process.exit(1);
  }, 8000).unref();
}

// Only auto-listen when run directly (not when imported by tests)
if (require.main === module) {
  if (process.env.NODE_ENV === 'production' && corsOrigin === DEV_ORIGIN) {
    console.warn(
      '[server] ⚠ CORS_ORIGIN is unset, so only http://localhost:5173 is allowed. ' +
      'Every deployed browser client will be refused. Set it to the webclient origin: ' +
      'fly secrets set CORS_ORIGIN=https://your-site.netlify.app',
    );
  }

  httpServer.listen(PORT, () => {
    console.log(`Salem 1692 server listening on port ${PORT} (CORS origin: ${corsOrigin})`);
  });

  process.on('SIGTERM', () => shutdown('SIGTERM'));
  process.on('SIGINT', () => shutdown('SIGINT'));
}

module.exports = { app, httpServer, io };
