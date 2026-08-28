/*
Schema export helper (run in Azure Data Studio against the target database)

Goal: paste results into:
- WebApp/AI/Knowledge/db/jeeves/sql-agent.md
- WebApp/AI/Knowledge/db/jeeves/joins.md

If this fails, tell us the engine (Azure SQL / Fabric / Synapse) and we’ll adapt the script.
*/

-- 1) Engine/version
SELECT @@VERSION AS SqlVersion;

-- 1b) Foreign key constraints present?
-- Note: Many warehouses/demo DBs have 0 FK constraints; joins are then "logical" instead of enforced.
SELECT COUNT(*) AS ForeignKeyCount FROM sys.foreign_keys;

-- 2) Top tables by row count (approx)
SELECT TOP (50)
    s.name  AS SchemaName,
    t.name  AS TableName,
    SUM(p.rows) AS TotalRows
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
GROUP BY s.name, t.name
ORDER BY SUM(p.rows) DESC;

-- 2b) All tables (helps spot fact tables)
SELECT
    s.name AS SchemaName,
    t.name AS TableName
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
ORDER BY s.name, t.name;

-- 3) Columns + data types + nullability + PK flag
WITH pks AS (
    SELECT
        ic.object_id,
        c.column_id,
        c.name AS ColumnName
    FROM sys.index_columns ic
    JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.is_primary_key = 1
)
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id AS ColumnId,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.precision AS [Precision],
    c.scale AS [Scale],
    c.is_nullable AS IsNullable,
    CASE WHEN pks.ColumnName IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN pks ON pks.object_id = t.object_id AND pks.column_id = c.column_id
ORDER BY s.name, t.name, c.column_id;

-- 3b) Likely join keys (heuristic) - columns that look like IDs/keys
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.is_nullable AS IsNullable
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE
    LOWER(c.name) LIKE '%id'
    OR LOWER(c.name) LIKE '%_id'
    OR LOWER(c.name) LIKE '%key'
    OR LOWER(c.name) LIKE '%_key'
ORDER BY s.name, t.name, c.name;

-- 4) Foreign keys (relationships)
SELECT
    sch_from.name AS FromSchema,
    t_from.name   AS FromTable,
    c_from.name   AS FromColumn,
    sch_to.name   AS ToSchema,
    t_to.name     AS ToTable,
    c_to.name     AS ToColumn
FROM sys.foreign_key_columns fkc
JOIN sys.tables t_from ON t_from.object_id = fkc.parent_object_id
JOIN sys.schemas sch_from ON sch_from.schema_id = t_from.schema_id
JOIN sys.columns c_from ON c_from.object_id = fkc.parent_object_id AND c_from.column_id = fkc.parent_column_id
JOIN sys.tables t_to ON t_to.object_id = fkc.referenced_object_id
JOIN sys.schemas sch_to ON sch_to.schema_id = t_to.schema_id
JOIN sys.columns c_to ON c_to.object_id = fkc.referenced_object_id AND c_to.column_id = fkc.referenced_column_id
ORDER BY FromSchema, FromTable, FromColumn;
