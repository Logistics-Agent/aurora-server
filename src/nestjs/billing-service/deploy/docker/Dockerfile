# Build stage
FROM node:20-alpine AS build
WORKDIR /app

COPY src/nestjs/billing-service/package*.json ./
COPY src/nestjs/billing-service/prisma ./prisma/
RUN npm ci

COPY protos /protos
COPY src/nestjs/billing-service/ ./
RUN npx prisma generate
RUN npm run build

# Runtime stage
FROM node:20-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

COPY src/nestjs/billing-service/package*.json ./
RUN npm ci --only=production && npm cache clean --force

COPY --from=build /app/dist ./dist
COPY --from=build /app/node_modules/.prisma ./node_modules/.prisma
COPY --from=build /app/prisma ./prisma
COPY --from=build /protos /protos

USER node
EXPOSE 5004 8080
CMD ["node", "dist/main"]
