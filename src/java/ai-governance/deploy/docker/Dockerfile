# syntax=docker/dockerfile:1.7

FROM maven:3.9.6-eclipse-temurin-21 AS build

WORKDIR /workspace

COPY protos/ /workspace/protos/

COPY src/java/shared/pom.xml \
    /workspace/src/java/shared/pom.xml

COPY src/java/shared/src/ \
    /workspace/src/java/shared/src/

RUN --mount=type=cache,target=/root/.m2 \
    mvn -f /workspace/src/java/shared/pom.xml \
    install -DskipTests -B

RUN --mount=type=cache,target=/root/.m2 \
    test -f \
    /root/.m2/repository/com/aurora/aurora-java-shared/1.0.0-SNAPSHOT/aurora-java-shared-1.0.0-SNAPSHOT.jar

COPY src/java/ai-governance/pom.xml \
    /workspace/src/java/ai-governance/pom.xml

RUN --mount=type=cache,target=/root/.m2 \
    mvn -f /workspace/src/java/ai-governance/pom.xml \
    dependency:go-offline -DskipTests -B

COPY src/java/ai-governance/src/ \
    /workspace/src/java/ai-governance/src/

RUN --mount=type=cache,target=/root/.m2 \
    mvn -f /workspace/src/java/ai-governance/pom.xml \
    package -DskipTests -B


FROM eclipse-temurin:21-jre-alpine AS runtime

WORKDIR /app

RUN addgroup -S appgroup \
    && adduser -S appuser -G appgroup

COPY --from=build \
    /workspace/src/java/ai-governance/target/*.jar \
    /app/app.jar

USER appuser

EXPOSE 8080

ENV JAVA_OPTS="-XX:+UseContainerSupport -XX:MaxRAMPercentage=75.0"

ENTRYPOINT ["sh", "-c", "java $JAVA_OPTS -jar /app/app.jar"]