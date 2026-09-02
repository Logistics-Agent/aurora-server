# Build stage
FROM node:20-alpine AS build
WORKDIR /app

COPY src/nestjs/realtime-hub-service/package*.json ./
RUN npm ci

COPY protos /protos
COPY src/nestjs/realtime-hub-service/ ./
RUN npm run build

# Runtime stage
FROM node:20-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

COPY src/nestjs/realtime-hub-service/package*.json ./
RUN npm ci --only=production && npm cache clean --force

COPY --from=build /app/dist ./dist
COPY --from=build /protos /protos

USER node
EXPOSE 5008 8080
CMD ["node", "dist/main"]
