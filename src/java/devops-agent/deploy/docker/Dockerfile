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


COPY src/java/devops-agent/pom.xml \
    /workspace/src/java/devops-agent/pom.xml

# Optional optimization
RUN mvn -f /workspace/src/java/devops-agent/pom.xml \
    dependency:go-offline -B

COPY src/java/devops-agent/src/ \
    /workspace/src/java/devops-agent/src/

RUN mvn -f /workspace/src/java/devops-agent/pom.xml \
    clean package -DskipTests -B

FROM eclipse-temurin:21-jre-alpine AS runtime

WORKDIR /app

RUN addgroup -S appgroup \
    && adduser -S appuser -G appgroup

COPY --from=build \
    /workspace/src/java/devops-agent/target/*.jar \
    /app/app.jar

USER appuser

EXPOSE 8080

ENV JAVA_OPTS="-XX:+UseContainerSupport -XX:MaxRAMPercentage=75.0"

ENTRYPOINT ["sh", "-c", "java $JAVA_OPTS -jar /app/app.jar"]