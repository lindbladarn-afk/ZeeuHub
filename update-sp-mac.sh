#!/bin/bash
set -euo pipefail

CONTAINER="jeevesdb"
SA_PASSWORD="${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD in your local environment before running this script.}"
SQL_ROOT="./SQL"

copy_files() {
  local source_path="$1"
  local target_path="$2"

  echo "📦 Copying $(basename "$source_path") into container..."
  docker exec "$CONTAINER" sh -lc "mkdir -p '$target_path'"
  docker cp "${source_path}/." "$CONTAINER":"$target_path/"
}

run_batch() {
  local db="$1"
  local path="$2"
  local file

  echo "▶️  Applying to DB: $db from $path"
  while IFS= read -r file; do
    base=$(basename "$file")
    echo "  ⚙️  $base"

    if ! docker exec -i "$CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost \
      -U sa \
      -P "$SA_PASSWORD" \
      -C \
      -d "$db" \
      -b \
      -i "$file" </dev/null; then
      echo "  ❗️ Failed: $base"
    fi
  done < <(docker exec "$CONTAINER" sh -lc "ls -1 ${path}/*.sql 2>/dev/null")
}

copy_files "${SQL_ROOT}/AzureDb/StoredProcedures" "/usr/databasebackupfile/AzureSP"
copy_files "${SQL_ROOT}/JeevesDb/StoredProcedures" "/usr/databasebackupfile/JeevesSP"

echo "✅ Files copied. Now applying..."
run_batch "CustomerPortal" "/usr/databasebackupfile/AzureSP"
run_batch "Jeeves6" "/usr/databasebackupfile/JeevesSP"
echo "🎉 Stored procedures update done."
