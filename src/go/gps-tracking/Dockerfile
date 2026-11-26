FROM golang:1.22-alpine AS builder

WORKDIR /app
COPY go.mod ./
COPY . ./
RUN go mod download
RUN CGO_ENABLED=0 GOOS=linux go build -o /bin/gps-tracking-server ./cmd/server

FROM alpine:3.19
RUN apk --no-cache add ca-certificates tzdata
COPY --from=builder /bin/gps-tracking-server /usr/local/bin/gps-tracking-server

EXPOSE 6002
ENTRYPOINT ["gps-tracking-server"]

