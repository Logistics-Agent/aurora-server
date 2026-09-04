# syntax=docker/dockerfile:1

FROM maven:3.9.6-eclipse-temurin-17 AS build

WORKDIR /workspace

COPY protos/ /workspace/protos/

COPY src/java/audit-service/pom.xml \
    /workspace/src/java/audit-service/pom.xml

RUN --mount=type=cache,target=/root/.m2 \
    mvn -f /workspace/src/java/audit-service/pom.xml \
    dependency:go-offline -B

COPY src/java/audit-service/src/ \
    /workspace/src/java/audit-service/src/

RUN --mount=type=cache,target=/root/.m2 \
    mvn -f /workspace/src/java/audit-service/pom.xml \
    clean package -DskipTests -B

FROM eclipse-temurin:17-jre-alpine AS runtime

WORKDIR /app

RUN addgroup -S appgroup \
    && adduser -S appuser -G appgroup

COPY --from=build \
    /workspace/src/java/audit-service/target/*.jar \
    /app/app.jar    

USER appuser

EXPOSE 8080

ENTRYPOINT ["java", "-jar", "/app/app.jar"]