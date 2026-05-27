'use strict';

const express = require('express');
const { createServer } = require('http');
const { Server } = require('socket.io');
const cors = require('cors');
const { registerDispatch } = require('./dispatch');

const app = express();
const httpServer = createServer(app);

const corsOrigin = process.env.CORS_ORIGIN || 'http://localhost:5173';

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

// Only auto-listen when run directly (not when imported by tests)
if (require.main === module) {
  httpServer.listen(PORT, () => {
    console.log(`Salem 1692 server listening on port ${PORT}`);
  });
}

module.exports = { app, httpServer, io };
