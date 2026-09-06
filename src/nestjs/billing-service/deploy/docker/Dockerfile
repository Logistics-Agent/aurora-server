# =========================
# Build stage
# =========================
FROM node:22-bookworm-slim AS build

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    openssl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY src/nestjs/billing-service/package*.json ./

RUN npm ci

COPY src/nestjs/billing-service/prisma ./prisma
COPY protos /protos
COPY src/nestjs/billing-service/ ./

RUN npx prisma generate
RUN npm run build

# Giữ lại production dependencies בלבד
RUN npm prune --omit=dev \
    && npm cache clean --force


# =========================
# Runtime stage
# =========================
FROM node:22-bookworm-slim AS runtime

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    openssl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

ENV NODE_ENV=production

COPY --from=build --chown=node:node /app/package*.json ./
COPY --from=build --chown=node:node /app/dist ./dist
COPY --from=build --chown=node:node /app/node_modules ./node_modules
COPY --from=build --chown=node:node /app/prisma ./prisma
COPY --from=build --chown=node:node /protos /protos

USER node

EXPOSE 5004 8080

CMD ["npm", "run", "start:prod"]