# net-perform

`docker-compose.infra.yml`'in sadeleştirilmiş hali — sadece performans/gözlemlenebilirlik
için gereken 5 servisi içerir: **postgres, seq, otel-collector, prometheus, grafana**.

## Orijinalden Farklar

| Konu | Detay |
|---|---|
| Kaldırılan servisler | redis, kafka, kafka-ui, vault, keycloak, consul, rabbitmq, nginx, **jaeger** |
| Container isimleri | Talep edildiği gibi başına `permon-` eklendi (örn. `permon-neominal-postgres`). **Servis adları** (postgres, seq, otel-collector, prometheus, grafana) DEĞİŞMEDİ — iç DNS çözümlemesi servis adı üzerinden çalıştığı için provisioning dosyalarında adres güncellemesi gerekmedi. |
| Proje adı | Compose dosyasının başındaki `name:` alanı `net-perform` olarak ayarlandı |
| Network adı | `neominal-net` olarak KORUNDU — böylece mevcut `docker-compose.apps.yml`'iniz varsa ona dokunmadan bu stack'i ayrı/birlikte çalıştırabilirsiniz |

## Jaeger Kaldırılınca Ne Değişti?

`infra/otel-collector/otel-collector-config.yaml`'daki trace pipeline'ı artık
sadece `debug` exporter'ına yazıyor (trace'ler collector loglarında görünür,
ama görsel bir arayüzde izlenemez). Jaeger'ı geri isterseniz:
1. Jaeger servisini docker-compose'a geri ekleyin
2. Config'e şunu geri ekleyin:
   ```yaml
   otlp_grpc/jaeger:
     endpoint: jaeger:4317
     tls:
       insecure: true
   ```
3. `traces` pipeline'ının `exporters` listesine `otlp_grpc/jaeger`'ı ekleyin

## Prometheus Job'ları

`infra/prometheus/prometheus.yml` **gerçek dosyanızdan** alınmıştır — sadece
`consul` job'u kaldırılmıştır (Consul bu sette olmadığı için sürekli DOWN
görünen anlamsız bir hedef olurdu). Diğer job'lar (order-api, payment-api,
inventory-api, api-gateway, job-service) hem Docker hem `host.docker.internal`
(yerel `dotnet run`) modunu destekliyor — bu servisleri `docker-compose.apps.yml`
ile veya doğrudan `dotnet run` ile ayrı çalıştırabilirsiniz.

## ⚠️ Yeniden Oluşturulan Dosya

`infra/postgres/init-multiple-dbs.sh` **orijinal repo dosyası değildir** —
tarafıma yüklenmediği için `docker-compose.infra.yml`'deki yorumlardan yola
çıkılarak yeniden oluşturulmuştur (order_db, payment_db, inventory_db,
hangfire_db oluşturur). Elinizde gerçek dosya varsa onu bu dosyanın
yerine koyun.

## Çalıştırma

```bash
cp .env.example .env
# .env içindeki şifreleri güncelleyin
docker compose -f docker-compose.net-permon.yml up -d
```

| Servis | Adres |
|---|---|
| Postgres | localhost:15432 |
| Seq | http://localhost:8081 |
| OTel Collector (OTLP) | localhost:4317 (gRPC) / localhost:4318 (HTTP) |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3000 |
