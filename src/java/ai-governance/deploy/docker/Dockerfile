# syntax=docker/dockerfile:1

FROM maven:3.9.6-eclipse-temurin-21 AS build

WORKDIR /workspace

COPY protos/ /workspace/protos/

COPY src/java/shared/pom.xml \
    /workspace/src/java/shared/pom.xml

COPY src/java/shared/src/ \
    /workspace/src/java/shared/src/

RUN mvn -f /workspace/src/java/shared/pom.xml \
    clean install -DskipTests -B

RUN test -f \
    /root/.m2/repository/com/aurora/aurora-java-shared/1.0.0-SNAPSHOT/aurora-java-shared-1.0.0-SNAPSHOT.jar


COPY src/java/ai-governance/pom.xml \
    /workspace/src/java/ai-governance/pom.xml

RUN mvn -f /workspace/src/java/ai-governance/pom.xml \
    dependency:go-offline -B

COPY src/java/ai-governance/src/ \
    /workspace/src/java/ai-governance/src/

RUN mvn -f /workspace/src/java/ai-governance/pom.xml \
    clean package -DskipTests -B


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