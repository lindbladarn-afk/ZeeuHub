#!/usr/bin/env bash
# Exercises local Hub database backup metadata, validation and destructive-operation guards.
set -Eeuo pipefail

readonly TEST_DIRECTORY="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
readonly SCRIPT_PATH="$(cd "${TEST_DIRECTORY}/.." && pwd -P)/jeeves-db.sh"
readonly REPOSITORY_ROOT="$(cd "${TEST_DIRECTORY}/../.." && pwd -P)"
readonly TEMP_DIRECTORY="$(mktemp -d)"
readonly MOCK_BACKUP_DIRECTORY="${REPOSITORY_ROOT}/JeevesDatabase/DatabaseBackup"
readonly MOCK_JEEVES_BACKUP_PATH="${MOCK_BACKUP_DIRECTORY}/Jeeves6-script-test.bak"
readonly MOCK_PORTAL_BACKUP_PATH="${MOCK_BACKUP_DIRECTORY}/CustomerPortal-script-test.bak"
readonly MOCK_ALL_JEEVES_BACKUP_PATH="${MOCK_BACKUP_DIRECTORY}/Jeeves6-local-script-all.bak"
readonly MOCK_ALL_PORTAL_BACKUP_PATH="${MOCK_BACKUP_DIRECTORY}/CustomerPortal-local-script-all.bak"

cleanup() {
    rm -f \
        "${MOCK_JEEVES_BACKUP_PATH}" \
        "${MOCK_PORTAL_BACKUP_PATH}" \
        "${MOCK_ALL_JEEVES_BACKUP_PATH}" \
        "${MOCK_ALL_PORTAL_BACKUP_PATH}"
    rm -rf "${TEMP_DIRECTORY}"
}
trap cleanup EXIT

assert_failure_contains() {
    local expected="$1"
    shift
    local output

    if output="$("$@" 2>&1)"; then
        printf 'Expected command to fail: %s\n' "$*" >&2
        exit 1
    fi

    [[ "${output}" == *"${expected}"* ]] || {
        printf 'Expected failure to contain "%s" but got:\n%s\n' "${expected}" "${output}" >&2
        exit 1
    }
}

bash -n "${SCRIPT_PATH}"
assert_failure_contains "Unknown command" "${SCRIPT_PATH}" unsupported
assert_failure_contains "must not contain a directory path" "${SCRIPT_PATH}" backup ../unsafe.bak
assert_failure_contains "must end in .bak" "${SCRIPT_PATH}" backup unsafe.sql
assert_failure_contains "does not exist" "${SCRIPT_PATH}" restore missing.bak

touch "${TEMP_DIRECTORY}/outside.bak"
printf 'not-empty\n' > "${TEMP_DIRECTORY}/outside.bak"
assert_failure_contains "must be placed in" "${SCRIPT_PATH}" restore "${TEMP_DIRECTORY}/outside.bak"

grep -q "COPY_ONLY" "${SCRIPT_PATH}"
grep -q "CHECKSUM" "${SCRIPT_PATH}"
grep -q "RESTORE VERIFYONLY" "${SCRIPT_PATH}"
grep -q "SET SINGLE_USER WITH ROLLBACK IMMEDIATE" "${SCRIPT_PATH}"
grep -q 'unless --replace' "${SCRIPT_PATH}"
if grep -q 'sqlcmd.*-P' "${SCRIPT_PATH}"; then
    printf 'The SQL password must not be passed as a command-line argument.\n' >&2
    exit 1
fi

mkdir -p "${TEMP_DIRECTORY}/bin" "${MOCK_BACKUP_DIRECTORY}"
cat > "${TEMP_DIRECTORY}/bin/docker" <<'MOCK_DOCKER'
#!/usr/bin/env bash
# Mimics the Docker responses needed for the script's non-destructive control-flow tests.
set -Eeuo pipefail

case "${1:-}" in
    info)
        exit 0
        ;;
    compose)
        exit 0
        ;;
    exec)
        IFS= read -r supplied_password
        [[ -n "${supplied_password}" ]]
        arguments="$*"
        if [[ "${arguments}" == *"SELECT CASE WHEN DB_ID"* ]]; then
            printf '%s\n' "${MOCK_DATABASE_EXISTS:-1}"
        fi
        if [[ "${arguments}" =~ /usr/databasebackupfile/([A-Za-z0-9][A-Za-z0-9._-]*\.bak) ]]; then
            backup_filename="${BASH_REMATCH[1]}"
        else
            backup_filename=""
        fi
        if [[ "${arguments}" == *"BACKUP DATABASE"* && -n "${backup_filename}" ]]; then
            printf 'mock backup payload\n' > "${MOCK_BACKUP_DIRECTORY}/${backup_filename}"
        fi
        if [[ "${arguments}" == *"RESTORE HEADERONLY"* ]]; then
            if [[ "${backup_filename}" == CustomerPortal-* ]]; then
                printf '1|2|3|4|5|6|7|8|9|CustomerPortal\n'
            else
                printf '1|2|3|4|5|6|7|8|9|Jeeves6\n'
            fi
        fi
        ;;
    *)
        printf 'Unexpected mock docker command: %s\n' "$*" >&2
        exit 1
        ;;
esac
MOCK_DOCKER
chmod +x "${TEMP_DIRECTORY}/bin/docker"

export PATH="${TEMP_DIRECTORY}/bin:${PATH}"
export MSSQL_SA_PASSWORD="LocalOnly-Test123!"
export MOCK_BACKUP_DIRECTORY
export MOCK_DATABASE_EXISTS=1
export LOCAL_HUB_BACKUP_TIMESTAMP="script-all"

"${SCRIPT_PATH}" backup jeeves6 "$(basename "${MOCK_JEEVES_BACKUP_PATH}")" >/dev/null
"${SCRIPT_PATH}" backup customerportal "$(basename "${MOCK_PORTAL_BACKUP_PATH}")" >/dev/null
"${SCRIPT_PATH}" backup all >/dev/null
"${SCRIPT_PATH}" verify "${MOCK_JEEVES_BACKUP_PATH}" >/dev/null
"${SCRIPT_PATH}" verify "${MOCK_PORTAL_BACKUP_PATH}" >/dev/null

assert_failure_contains "already exists" "${SCRIPT_PATH}" restore jeeves6 "${MOCK_JEEVES_BACKUP_PATH}"
assert_failure_contains "belongs to Jeeves6" "${SCRIPT_PATH}" restore customerportal "${MOCK_JEEVES_BACKUP_PATH}" --replace
assert_failure_contains "A local Hub database already exists" \
    "${SCRIPT_PATH}" restore all "${MOCK_PORTAL_BACKUP_PATH}" "${MOCK_JEEVES_BACKUP_PATH}"

"${SCRIPT_PATH}" restore customerportal "${MOCK_PORTAL_BACKUP_PATH}" --replace >/dev/null
"${SCRIPT_PATH}" restore jeeves6 "${MOCK_JEEVES_BACKUP_PATH}" --replace >/dev/null
"${SCRIPT_PATH}" restore all "${MOCK_PORTAL_BACKUP_PATH}" "${MOCK_JEEVES_BACKUP_PATH}" --replace >/dev/null

printf 'All Jeeves database script tests passed.\n'
