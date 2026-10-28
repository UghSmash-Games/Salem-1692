# Salem 1692 relay server.
#
# Node 22 (LTS), not the newest release: the test runners break on Node 25 (see CLAUDE.md →
# Testing), and a production image should not be the first place that discrepancy shows up.
#
# Alpine keeps the image small; the relay has no native dependencies to complicate it.
FROM node:22-alpine

WORKDIR /app

# Copy manifests first so `npm ci` is cached and only re-runs when dependencies actually change.
COPY package*.json ./

# --omit=dev: jest and socket.io-client are test-only. Nothing in src/ imports them.
RUN npm ci --omit=dev

COPY src ./src

# Run as the unprivileged user the base image already provides, rather than root.
USER node

# Fly injects PORT; this is the fallback and the documented default.
ENV PORT=3000
EXPOSE 3000

CMD ["node", "src/index.js"]
