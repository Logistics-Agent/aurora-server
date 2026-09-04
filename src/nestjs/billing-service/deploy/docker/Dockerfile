# Build stage
FROM node:20-bookworm-slim AS build
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends openssl ca-certificates \
    && rm -rf /var/lib/apt/lists/*


COPY src/nestjs/billing-service/package*.json ./
COPY src/nestjs/billing-service/prisma ./prisma/
RUN npm ci

COPY protos /protos
COPY src/nestjs/billing-service/ ./
RUN npx prisma generate
RUN npm run build

# Runtime stage
FROM node:20-bookworm-slim AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends openssl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

ENV NODE_ENV=production

COPY src/nestjs/billing-service/package*.json ./
RUN npm ci --only=production && npm cache clean --force

COPY --from=build /app/dist ./dist
COPY --from=build /app/node_modules/.prisma ./node_modules/.prisma
COPY --from=build /app/prisma ./prisma
COPY --from=build /protos /protos

USER node
EXPOSE 5004 8080
CMD ["npm", "run", "start:prod"]