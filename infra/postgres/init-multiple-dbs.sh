#!/bin/bash
# ============================================================================
# NOT: Bu script'in ORİJİNALİ tarafımıza yüklenmedi (repodaki gerçek dosya
# değil). docker-compose.infra.yml'deki şu yorumdan yola çıkılarak yeniden
# oluşturulmuştur:
#
#   "Modül 4 - Database per Service: order_db, payment_db, inventory_db,
#    hangfire_db veritabanlarını POSTGRES_DB (neominal_demo) yanına EK
#    olarak oluşturur. Script $POSTGRES_USER'ı (neominal) kullanır."
#
# Elinizde repodaki ORİJİNAL infra/postgres/init-multiple-dbs.sh varsa,
# onu bu dosyanın yerine koyun — aralarında küçük farklar olabilir.
# ============================================================================
set -e

DATABASES=(order_db payment_db inventory_db hangfire_db)

for db in "${DATABASES[@]}"; do
  echo "[init-multiple-dbs] '$db' veritabani olusturuluyor (varsa atlanir)..."
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    SELECT 'CREATE DATABASE $db OWNER $POSTGRES_USER'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db')\gexec
EOSQL
done

echo "[init-multiple-dbs] Tamamlandi: ${DATABASES[*]}"
