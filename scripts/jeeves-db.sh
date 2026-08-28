#!/usr/bin/env bash
# Creates, verifies and restores local CustomerPortal and Jeeves6 development backups.
set -Eeuo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
readonly REPOSITORY_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd -P)"
readonly BACKUP_DIRECTORY="${REPOSITORY_ROOT}/JeevesDatabase/DatabaseBackup"
readonly CONTAINER_BACKUP_DIRECTORY="/usr/databasebackupfile"
readonly CONTAINER_NAME="${JEEVES_DB_CONTAINER_NAME:-jeevesdb}"
readonly PORTAL_DATABASE_NAME="CustomerPortal"
readonly JEEVES_DATABASE_NAME="Jeeves6"
SQL_SERVER_PASSWORD=""

usage() {
    cat <<'USAGE'
Usage:
  ./scripts/jeeves-db.sh backup [jeeves6|customerportal] [filename.bak] [--overwrite]
  ./scripts/jeeves-db.sh backup all [--overwrite]
  ./scripts/jeeves-db.sh verify <backup-file.bak>
  ./scripts/jeeves-db.sh restore [jeeves6|customerportal] <backup-file.bak> [--replace]
  ./scripts/jeeves-db.sh restore all <CustomerPortal.bak> <Jeeves6.bak> [--replace]
  ./scripts/jeeves-db.sh status

Backup files are stored in JeevesDatabase/DatabaseBackup. Restore validates that
each file belongs to the selected database and refuses to replace an existing
database unless --replace is supplied explicitly.
USAGE
}

fail() {
    printf 'Error: %s\n' "$1" >&2
    exit 1
}

compose() {
    (
        cd "${REPOSITORY_ROOT}"
        docker compose "$@"
    )
}

validate_backup_name() {
    local filename="$1"

    [[ "${filename}" == "$(basename "${filename}")" ]] ||
        fail "Backup name must not contain a directory path: ${filename}"
    [[ "${filename}" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*\.bak$ ]] ||
        fail "Backup name must end in .bak and only contain letters, numbers, dot, dash or underscore."
}

normalize_database_name() {
    local requested_name
    requested_name="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"

    case "${requested_name}" in
        customerportal)
            printf '%s\n' "${PORTAL_DATABASE_NAME}"
            ;;
        jeeves6)
            printf '%s\n' "${JEEVES_DATABASE_NAME}"
            ;;
        *)
            fail "Unknown database: $1. Use customerportal or jeeves6."
            ;;
    esac
}

is_database_selector() {
    local requested_name
    requested_name="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
    [[ "${requested_name}" == "customerportal" || "${requested_name}" == "jeeves6" ]]
}

prepare_backup_directory() {
    mkdir -p "${BACKUP_DIRECTORY}"
}

resolve_backup_file() {
    local requested_path="$1"
    local candidate
    local candidate_directory
    local expected_directory

    prepare_backup_directory

    if [[ "${requested_path}" = /* ]]; then
        candidate="${requested_path}"
    elif [[ -f "${REPOSITORY_ROOT}/${requested_path}" ]]; then
        candidate="${REPOSITORY_ROOT}/${requested_path}"
    else
        candidate="${BACKUP_DIRECTORY}/${requested_path}"
    fi

    [[ -f "${candidate}" ]] || fail "Backup file does not exist: ${requested_path}"
    [[ -s "${candidate}" ]] || fail "Backup file is empty: ${requested_path}"

    candidate_directory="$(cd "$(dirname "${candidate}")" && pwd -P)"
    expected_directory="$(cd "${BACKUP_DIRECTORY}" && pwd -P)"
    [[ "${candidate_directory}" == "${expected_directory}" ]] ||
        fail "Backup file must be placed in ${BACKUP_DIRECTORY}."

    validate_backup_name "$(basename "${candidate}")"
    printf '%s\n' "${candidate_directory}/$(basename "${candidate}")"
}

validate_local_configuration() {
    local env_file="${REPOSITORY_ROOT}/.env"
    SQL_SERVER_PASSWORD="${MSSQL_SA_PASSWORD:-}"

    if [[ -z "${SQL_SERVER_PASSWORD}" && -f "${env_file}" ]]; then
        SQL_SERVER_PASSWORD="$(sed -n 's/^MSSQL_SA_PASSWORD=//p' "${env_file}" | tail -n 1 | tr -d '\r')"
    fi

    if [[ "${SQL_SERVER_PASSWORD}" == \"*\" && "${SQL_SERVER_PASSWORD}" == *\" ]]; then
        SQL_SERVER_PASSWORD="${SQL_SERVER_PASSWORD:1:${#SQL_SERVER_PASSWORD}-2}"
    elif [[ "${SQL_SERVER_PASSWORD}" == \'*\' && "${SQL_SERVER_PASSWORD}" == *\' ]]; then
        SQL_SERVER_PASSWORD="${SQL_SERVER_PASSWORD:1:${#SQL_SERVER_PASSWORD}-2}"
    fi

    [[ -n "${SQL_SERVER_PASSWORD}" ]] ||
        fail "Create .env from .env.example and configure MSSQL_SA_PASSWORD first."
    [[ "${SQL_SERVER_PASSWORD}" != "CHANGE_ME" ]] ||
        fail "Replace CHANGE_ME for MSSQL_SA_PASSWORD in .env first."
}

sqlcmd() {
    printf '%s\n' "${SQL_SERVER_PASSWORD}" | docker exec -i "${CONTAINER_NAME}" /bin/bash -lc '
        set -Eeuo pipefail
        IFS= read -r password
        [[ -n "${password}" ]] || {
            printf "SQL Server password was not supplied.\n" >&2
            exit 1
        }

        sqlcmd_path=""
        for candidate in /opt/mssql-tools18/bin/sqlcmd /opt/mssql-tools/bin/sqlcmd; do
            if [[ -x "${candidate}" ]]; then
                sqlcmd_path="${candidate}"
                break
            fi
        done

        [[ -n "${sqlcmd_path}" ]] || {
            printf "sqlcmd was not found inside the SQL Server container.\n" >&2
            exit 1
        }

        SQLCMDPASSWORD="${password}" exec "${sqlcmd_path}" -S localhost -U sa -C -b "$@"
    ' _ "$@"
}

ensure_database_container() {
    local attempt

    command -v docker >/dev/null 2>&1 || fail "Docker is not installed or is not available in PATH."
    docker info >/dev/null 2>&1 || fail "Docker Desktop is not running."
    validate_local_configuration
    prepare_backup_directory
    compose config --quiet
    compose up -d jeevesdb

    for attempt in {1..45}; do
        if sqlcmd -Q "SET NOCOUNT ON; SELECT 1;" >/dev/null 2>&1; then
            return
        fi
        sleep 2
    done

    fail "SQL Server did not become ready within 90 seconds. Run 'docker compose logs jeevesdb'."
}

database_exists() {
    local database_name="$1"
    local result
    result="$(sqlcmd -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'${database_name}') IS NULL THEN 0 ELSE 1 END;")"
    result="${result//[[:space:]]/}"
    [[ "${result}" == "1" ]]
}

verify_container_backup() {
    local filename="$1"
    sqlcmd -Q "RESTORE VERIFYONLY FROM DISK = N'${CONTAINER_BACKUP_DIRECTORY}/${filename}' WITH CHECKSUM;"
}

backup_database_name() {
    local filename="$1"
    local database_name

    database_name="$(sqlcmd -h -1 -W -s "|" -Q "RESTORE HEADERONLY FROM DISK = N'${CONTAINER_BACKUP_DIRECTORY}/${filename}';" |
        awk -F '|' 'NF >= 10 { gsub(/^[[:space:]]+|[[:space:]]+$/, "", $10); print $10; exit }')"
    [[ -n "${database_name}" ]] || fail "Could not read the source database name from ${filename}."
    printf '%s\n' "${database_name}"
}

validate_backup_database() {
    local filename="$1"
    local expected_database="$2"
    local actual_database

    actual_database="$(backup_database_name "${filename}")"
    [[ "${actual_database}" == "${expected_database}" ]] ||
        fail "Backup ${filename} belongs to ${actual_database}, not ${expected_database}."
}

backup_database_file() {
    local database_name="$1"
    local filename="$2"
    local overwrite="$3"
    local host_path

    validate_backup_name "${filename}"
    prepare_backup_directory
    host_path="${BACKUP_DIRECTORY}/${filename}"

    if [[ -e "${host_path}" && "${overwrite}" != "--overwrite" ]]; then
        fail "Backup already exists. Choose another name or supply --overwrite explicitly."
    fi

    database_exists "${database_name}" ||
        fail "Database ${database_name} does not exist in container ${CONTAINER_NAME}."

    printf 'Creating COPY_ONLY backup of %s...\n' "${database_name}"
    sqlcmd -Q "BACKUP DATABASE [${database_name}] TO DISK = N'${CONTAINER_BACKUP_DIRECTORY}/${filename}' WITH COPY_ONLY, FORMAT, INIT, COMPRESSION, CHECKSUM, NAME = N'${database_name} local development backup', STATS = 10;"

    printf 'Verifying backup checksum...\n'
    verify_container_backup "${filename}"
    validate_backup_database "${filename}" "${database_name}"
    [[ -s "${host_path}" ]] || fail "SQL Server completed without creating a readable host backup."

    printf 'Backup ready: %s\n' "${host_path}"
    du -h "${host_path}"
}

backup_command() {
    local selector="${1:-jeeves6}"
    local database_name
    local filename
    local overwrite
    local timestamp
    local portal_filename
    local jeeves_filename

    if [[ "$(printf '%s' "${selector}" | tr '[:upper:]' '[:lower:]')" == "all" ]]; then
        shift
        overwrite="${1:-}"
        [[ $# -le 1 ]] || fail "backup all accepts only optional --overwrite."
        [[ -z "${overwrite}" || "${overwrite}" == "--overwrite" ]] ||
            fail "Unknown backup option: ${overwrite}"
        timestamp="${LOCAL_HUB_BACKUP_TIMESTAMP:-$(date '+%Y%m%d-%H%M%S')}"
        [[ "${timestamp}" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*$ ]] ||
            fail "LOCAL_HUB_BACKUP_TIMESTAMP contains unsupported characters."
        portal_filename="CustomerPortal-local-${timestamp}.bak"
        jeeves_filename="Jeeves6-local-${timestamp}.bak"
        ensure_database_container
        database_exists "${PORTAL_DATABASE_NAME}" ||
            fail "Database ${PORTAL_DATABASE_NAME} does not exist in container ${CONTAINER_NAME}."
        database_exists "${JEEVES_DATABASE_NAME}" ||
            fail "Database ${JEEVES_DATABASE_NAME} does not exist in container ${CONTAINER_NAME}."
        if [[ "${overwrite}" != "--overwrite" ]] &&
            { [[ -e "${BACKUP_DIRECTORY}/${portal_filename}" ]] || [[ -e "${BACKUP_DIRECTORY}/${jeeves_filename}" ]]; }; then
            fail "A backup with this timestamp already exists. Retry later or supply --overwrite explicitly."
        fi
        backup_database_file "${PORTAL_DATABASE_NAME}" "${portal_filename}" "${overwrite}"
        backup_database_file "${JEEVES_DATABASE_NAME}" "${jeeves_filename}" "${overwrite}"
        return
    fi

    if is_database_selector "${selector}"; then
        database_name="$(normalize_database_name "${selector}")"
        shift
    else
        database_name="${JEEVES_DATABASE_NAME}"
    fi

    filename="${1:-${database_name}-local-$(date '+%Y%m%d-%H%M%S').bak}"
    overwrite="${2:-}"
    [[ $# -le 2 ]] || fail "backup accepts a database, optional filename and optional --overwrite."
    [[ -z "${overwrite}" || "${overwrite}" == "--overwrite" ]] ||
        fail "Unknown backup option: ${overwrite}"

    validate_backup_name "${filename}"
    ensure_database_container
    backup_database_file "${database_name}" "${filename}" "${overwrite}"
}

verify_backup() {
    local host_path
    local filename

    [[ $# -eq 1 ]] || fail "verify requires exactly one backup file."
    host_path="$(resolve_backup_file "$1")"
    filename="$(basename "${host_path}")"
    ensure_database_container
    verify_container_backup "${filename}"
    printf 'Backup is readable and verified for %s: %s\n' "$(backup_database_name "${filename}")" "${host_path}"
}

restore_database_file() {
    local database_name="$1"
    local filename="$2"
    local restore_sql

    restore_sql="
USE [master];
IF DB_ID(N'${database_name}') IS NOT NULL
    ALTER DATABASE [${database_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
BEGIN TRY
    RESTORE DATABASE [${database_name}]
        FROM DISK = N'${CONTAINER_BACKUP_DIRECTORY}/${filename}'
        WITH REPLACE, RECOVERY, STATS = 10;
    ALTER DATABASE [${database_name}] SET MULTI_USER;
END TRY
BEGIN CATCH
    IF DB_ID(N'${database_name}') IS NOT NULL
        ALTER DATABASE [${database_name}] SET MULTI_USER;
    THROW;
END CATCH;"

    printf 'Restoring %s from %s...\n' "${database_name}" "${filename}"
    sqlcmd -Q "${restore_sql}"
    sqlcmd -Q "SET NOCOUNT ON; IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'${database_name}' AND state_desc = N'ONLINE') THROW 51000, '${database_name} was not restored online.', 1;"
    printf 'Restore complete. Database %s is online.\n' "${database_name}"
}

restore_single_database() {
    local database_name="$1"
    local requested_path="$2"
    local replace="$3"
    local host_path
    local filename

    host_path="$(resolve_backup_file "${requested_path}")"
    filename="$(basename "${host_path}")"
    ensure_database_container

    printf 'Verifying backup before restore...\n'
    verify_container_backup "${filename}"
    validate_backup_database "${filename}" "${database_name}"

    if database_exists "${database_name}" && [[ "${replace}" != "--replace" ]]; then
        fail "Database ${database_name} already exists. Re-run with --replace only when you intend to overwrite it."
    fi

    restore_database_file "${database_name}" "${filename}"
}

restore_all_databases() {
    local portal_path="$1"
    local jeeves_path="$2"
    local replace="$3"
    local portal_host_path
    local jeeves_host_path
    local portal_filename
    local jeeves_filename

    portal_host_path="$(resolve_backup_file "${portal_path}")"
    jeeves_host_path="$(resolve_backup_file "${jeeves_path}")"
    portal_filename="$(basename "${portal_host_path}")"
    jeeves_filename="$(basename "${jeeves_host_path}")"
    ensure_database_container

    printf 'Verifying both backups before either database is changed...\n'
    verify_container_backup "${portal_filename}"
    verify_container_backup "${jeeves_filename}"
    validate_backup_database "${portal_filename}" "${PORTAL_DATABASE_NAME}"
    validate_backup_database "${jeeves_filename}" "${JEEVES_DATABASE_NAME}"

    if [[ "${replace}" != "--replace" ]] &&
        { database_exists "${PORTAL_DATABASE_NAME}" || database_exists "${JEEVES_DATABASE_NAME}"; }; then
        fail "A local Hub database already exists. Re-run with --replace only when both local databases may be overwritten."
    fi

    restore_database_file "${PORTAL_DATABASE_NAME}" "${portal_filename}"
    restore_database_file "${JEEVES_DATABASE_NAME}" "${jeeves_filename}"
}

restore_command() {
    local selector="${1:-}"
    local database_name
    local requested_path
    local replace

    [[ -n "${selector}" ]] || fail "restore requires a database and backup file."

    if [[ "$(printf '%s' "${selector}" | tr '[:upper:]' '[:lower:]')" == "all" ]]; then
        [[ $# -ge 3 && $# -le 4 ]] ||
            fail "restore all requires CustomerPortal.bak, Jeeves6.bak and optional --replace."
        replace="${4:-}"
        [[ -z "${replace}" || "${replace}" == "--replace" ]] ||
            fail "Unknown restore option: ${replace}"
        restore_all_databases "$2" "$3" "${replace}"
        return
    fi

    if is_database_selector "${selector}"; then
        database_name="$(normalize_database_name "${selector}")"
        requested_path="${2:-}"
        replace="${3:-}"
        [[ $# -le 3 ]] || fail "restore accepts a database, backup file and optional --replace."
    else
        database_name="${JEEVES_DATABASE_NAME}"
        requested_path="${selector}"
        replace="${2:-}"
        [[ $# -le 2 ]] || fail "restore accepts a backup file and optional --replace."
    fi

    [[ -n "${requested_path}" ]] || fail "restore requires a backup file."
    [[ -z "${replace}" || "${replace}" == "--replace" ]] ||
        fail "Unknown restore option: ${replace}"

    restore_single_database "${database_name}" "${requested_path}" "${replace}"
}

show_status() {
    ensure_database_container
    sqlcmd -Q "SELECT name, state_desc, recovery_model_desc, compatibility_level FROM sys.databases WHERE name IN (N'${PORTAL_DATABASE_NAME}', N'${JEEVES_DATABASE_NAME}') ORDER BY name;"
}

main() {
    local command="${1:-}"
    [[ -n "${command}" ]] || {
        usage
        exit 1
    }
    shift

    case "${command}" in
        backup)
            backup_command "$@"
            ;;
        verify)
            verify_backup "$@"
            ;;
        restore)
            restore_command "$@"
            ;;
        status)
            [[ $# -eq 0 ]] || fail "status does not accept arguments."
            show_status
            ;;
        -h|--help|help)
            usage
            ;;
        *)
            usage >&2
            fail "Unknown command: ${command}"
            ;;
    esac
}

main "$@"
