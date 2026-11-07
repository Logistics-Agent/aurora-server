# ==============================================================================
# Build Stage
# ==============================================================================
FROM golang:1.22-alpine AS builder

WORKDIR /app

# Cài đặt git và certificates cần thiết
RUN apk add --no-cache git ca-certificates tzdata

# Copy mã nguồn Go (cả shared module và service)
COPY src/go/go.work src/go/go.work
COPY src/go/shared/ src/go/shared/
COPY src/go/route-planning-agent/ src/go/route-planning-agent/

WORKDIR /app/src/go/route-planning-agent

# Tải dependencies và biên dịch nhị phân tĩnh (static binary)
RUN go mod download
RUN CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build \
    -ldflags="-s -w" \
    -o /app/server \
    ./cmd/server

# ==============================================================================
# Runtime Stage (Lightweight ~20MB)
# ==============================================================================
FROM alpine:3.20 AS runtime

RUN apk add --no-cache ca-certificates tzdata

WORKDIR /app

# Copy executable từ build stage
COPY --from=builder /app/server /app/server

EXPOSE 5005

# Khởi chạy gRPC Service
ENTRYPOINT ["/app/server"]

