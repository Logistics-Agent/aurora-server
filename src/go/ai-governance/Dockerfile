FROM golang:1.22-alpine AS builder

WORKDIR /workspace
COPY src/go/shared ./shared
COPY src/go/ai-governance ./ai-governance

WORKDIR /workspace/ai-governance
RUN go mod download
RUN CGO_ENABLED=0 GOOS=linux go build -o /bin/ai-governance-server ./cmd/server

FROM alpine:3.19
RUN apk --no-cache add ca-certificates tzdata
COPY --from=builder /bin/ai-governance-server /usr/local/bin/ai-governance-server

EXPOSE 8080 9090
ENTRYPOINT ["ai-governance-server"]
