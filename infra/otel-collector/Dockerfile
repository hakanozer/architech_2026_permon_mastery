FROM otel/opentelemetry-collector:latest AS collector
FROM alpine:3.22
RUN apk add --no-cache ca-certificates
COPY --from=collector /otelcol /otelcol
ENTRYPOINT ["/otelcol"]