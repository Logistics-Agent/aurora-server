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

# Install dependencies first for Docker layer cache
COPY src/nestjs/customer-assistant-service/package*.json ./

RUN npm ci

# Prisma
COPY src/nestjs/customer-assistant-service/prisma ./prisma

# Proto definitions
COPY protos /protos

# Application source
COPY src/nestjs/customer-assistant-service/ ./

# Generate Prisma Client
RUN npx prisma generate

# Build NestJS
RUN npm run build

# Remove development dependencies
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

EXPOSE 5007 8080

CMD ["npm", "run", "start:prod"]