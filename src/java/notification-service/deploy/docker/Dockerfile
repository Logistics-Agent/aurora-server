# syntax=docker/dockerfile:1

FROM maven:3.9.6-eclipse-temurin-21-alpine AS build

WORKDIR /workspace

COPY protos/ /workspace/protos/

COPY src/java/notification-service/pom.xml \
    /workspace/src/java/notification-service/pom.xml

RUN --mount=type=cache,target=/root/.m2 \
    mvn -f /workspace/src/java/notification-service/pom.xml \
    dependency:go-offline -B

COPY src/java/notification-service/src/ \
    /workspace/src/java/notification-service/src/

RUN --mount=type=cache,target=/root/.m2 \
    mvn -f /workspace/src/java/notification-service/pom.xml \
    clean package -DskipTests -B

FROM eclipse-temurin:21-jre-alpine AS runtime

WORKDIR /app

RUN addgroup -S appgroup \
    && adduser -S appuser -G appgroup

COPY --from=build \
    /workspace/src/java/notification-service/target/*.jar \
    /app/app.jar

USER appuser

EXPOSE 8080

ENV JAVA_OPTS="-XX:+UseContainerSupport -XX:MaxRAMPercentage=75.0"

ENTRYPOINT ["sh", "-c", "java $JAVA_OPTS -jar /app/app.jar"]