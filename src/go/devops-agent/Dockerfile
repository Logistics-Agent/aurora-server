FROM golang:1.22-alpine AS builder

WORKDIR /workspace
COPY src/go/shared ./shared
COPY src/go/devops-agent ./devops-agent

WORKDIR /workspace/devops-agent
RUN go mod download
RUN CGO_ENABLED=0 GOOS=linux go build -o /bin/devops-agent-server ./cmd/server

FROM alpine:3.19
RUN apk --no-cache add ca-certificates tzdata
COPY --from=builder /bin/devops-agent-server /usr/local/bin/devops-agent-server

EXPOSE 8080 9090
ENTRYPOINT ["devops-agent-server"]
