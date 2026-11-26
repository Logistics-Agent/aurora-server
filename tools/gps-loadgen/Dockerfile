FROM golang:1.22-alpine AS builder

WORKDIR /app
COPY go.mod ./
COPY . ./
RUN go mod download
RUN CGO_ENABLED=0 GOOS=linux go build -o /bin/gps-loadgen .

FROM alpine:3.19
RUN apk --no-cache add ca-certificates tzdata
COPY --from=builder /bin/gps-loadgen /usr/local/bin/gps-loadgen

ENTRYPOINT ["gps-loadgen"]
CMD ["-profile", "C", "-duration", "300", "-seed", "42"]

