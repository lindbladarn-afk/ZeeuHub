/*
Importera gamla NotifyMe-data till portalens Azure DB.

Förutsätter att dessa tabeller redan finns i portalens DB:
- dbo.q_zu_notcenter
- dbo.q_zu_notcenter_varningstyp
- dbo.q_zu_notcenter_varningskat
- dbo.q_zu_notcenter_log

Rekommenderad väg:
1. exportera från Jeeves med NotifyMe.ExportFromJeeves.sql
2. importera till temporära staging-tabeller i Azure, t.ex.:
   - dbo.stg_q_zu_notcenter
   - dbo.stg_q_zu_notcenter_varningstyp
   - dbo.stg_q_zu_notcenter_varningskat
   - dbo.stg_q_zu_notcenter_log
3. kör den här filen
*/

SET NOCOUNT ON;

IF OBJECT_ID('dbo.stg_q_zu_notcenter', 'U') IS NULL
    THROW 50000, 'Stagingtabellen dbo.stg_q_zu_notcenter saknas.', 1;

IF OBJECT_ID('dbo.stg_q_zu_notcenter_varningstyp', 'U') IS NULL
    THROW 50000, 'Stagingtabellen dbo.stg_q_zu_notcenter_varningstyp saknas.', 1;

IF OBJECT_ID('dbo.stg_q_zu_notcenter_varningskat', 'U') IS NULL
    THROW 50000, 'Stagingtabellen dbo.stg_q_zu_notcenter_varningskat saknas.', 1;

IF OBJECT_ID('dbo.stg_q_zu_notcenter_log', 'U') IS NULL
    PRINT 'Stagingtabellen dbo.stg_q_zu_notcenter_log saknas. Historik kommer inte att importeras.';

MERGE dbo.q_zu_notcenter_varningstyp AS target
USING (
    SELECT
        q_zu_notcenter_typ,
        q_zu_notcenter_typbeskr
    FROM dbo.stg_q_zu_notcenter_varningstyp
) AS source
ON target.q_zu_notcenter_typ = source.q_zu_notcenter_typ
WHEN MATCHED THEN
    UPDATE SET
        target.q_zu_notcenter_typbeskr = source.q_zu_notcenter_typbeskr
WHEN NOT MATCHED BY TARGET THEN
    INSERT (
        q_zu_notcenter_typ,
        q_zu_notcenter_typbeskr
    )
    VALUES (
        source.q_zu_notcenter_typ,
        source.q_zu_notcenter_typbeskr
    );

MERGE dbo.q_zu_notcenter_varningskat AS target
USING (
    SELECT
        q_zu_notcenter_prio,
        q_zu_notcenter_typbeskr
    FROM dbo.stg_q_zu_notcenter_varningskat
) AS source
ON target.q_zu_notcenter_prio = source.q_zu_notcenter_prio
WHEN MATCHED THEN
    UPDATE SET
        target.q_zu_notcenter_typbeskr = source.q_zu_notcenter_typbeskr
WHEN NOT MATCHED BY TARGET THEN
    INSERT (
        q_zu_notcenter_prio,
        q_zu_notcenter_typbeskr
    )
    VALUES (
        source.q_zu_notcenter_prio,
        source.q_zu_notcenter_typbeskr
    );

MERGE dbo.q_zu_notcenter AS target
USING (
    SELECT
        q_zu_notcenter_nr,
        perssign,
        regdat,
        rowcreatedby,
        rowcreateddt,
        rowupdatedby,
        rowupdateddt,
        foretagkod,
        q_zu_notcenter_beskrivning,
        q_zu_notcenter_typ,
        q_zu_notcenter_prio,
        q_zu_notcenter_varntext,
        q_zu_notcenter_kommentar,
        q_zu_notcenter_mailadress1,
        q_zu_notcenter_mailadress2,
        q_zu_notcenter_schema,
        q_zu_notcenter_in_use,
        q_zu_notcenter_antvarning,
        q_zu_notcenter_execdat,
        q_zu_notcenter_varndat,
        q_zu_notcenter_select2,
        q_zu_notcenter_sysl,
        q_zu_notcenter_startdat,
        q_zu_notcenter_schedule,
        q_zu_notcenter_antal_eskalera,
        q_zu_notcenter_email_eskalera,
        q_zu_notcenter_bcc,
        q_zu_notcenter_cc,
        q_zu_notcenter_dyn_adress,
        q_zu_notcenter_language
    FROM dbo.stg_q_zu_notcenter
) AS source
ON target.q_zu_notcenter_nr = source.q_zu_notcenter_nr
AND target.foretagkod = source.foretagkod
WHEN MATCHED THEN
    UPDATE SET
        target.perssign = source.perssign,
        target.regdat = source.regdat,
        target.rowcreatedby = source.rowcreatedby,
        target.rowcreateddt = source.rowcreateddt,
        target.rowupdatedby = source.rowupdatedby,
        target.rowupdateddt = source.rowupdateddt,
        target.q_zu_notcenter_beskrivning = source.q_zu_notcenter_beskrivning,
        target.q_zu_notcenter_typ = source.q_zu_notcenter_typ,
        target.q_zu_notcenter_prio = source.q_zu_notcenter_prio,
        target.q_zu_notcenter_varntext = source.q_zu_notcenter_varntext,
        target.q_zu_notcenter_kommentar = source.q_zu_notcenter_kommentar,
        target.q_zu_notcenter_mailadress1 = source.q_zu_notcenter_mailadress1,
        target.q_zu_notcenter_mailadress2 = source.q_zu_notcenter_mailadress2,
        target.q_zu_notcenter_schema = source.q_zu_notcenter_schema,
        target.q_zu_notcenter_in_use = source.q_zu_notcenter_in_use,
        target.q_zu_notcenter_antvarning = source.q_zu_notcenter_antvarning,
        target.q_zu_notcenter_execdat = source.q_zu_notcenter_execdat,
        target.q_zu_notcenter_varndat = source.q_zu_notcenter_varndat,
        target.q_zu_notcenter_select2 = source.q_zu_notcenter_select2,
        target.q_zu_notcenter_sysl = source.q_zu_notcenter_sysl,
        target.q_zu_notcenter_startdat = source.q_zu_notcenter_startdat,
        target.q_zu_notcenter_schedule = source.q_zu_notcenter_schedule,
        target.q_zu_notcenter_antal_eskalera = source.q_zu_notcenter_antal_eskalera,
        target.q_zu_notcenter_email_eskalera = source.q_zu_notcenter_email_eskalera,
        target.q_zu_notcenter_bcc = source.q_zu_notcenter_bcc,
        target.q_zu_notcenter_cc = source.q_zu_notcenter_cc,
        target.q_zu_notcenter_dyn_adress = source.q_zu_notcenter_dyn_adress,
        target.q_zu_notcenter_language = source.q_zu_notcenter_language
WHEN NOT MATCHED BY TARGET THEN
    INSERT (
        q_zu_notcenter_nr,
        perssign,
        regdat,
        rowcreatedby,
        rowcreateddt,
        rowupdatedby,
        rowupdateddt,
        foretagkod,
        q_zu_notcenter_beskrivning,
        q_zu_notcenter_typ,
        q_zu_notcenter_prio,
        q_zu_notcenter_varntext,
        q_zu_notcenter_kommentar,
        q_zu_notcenter_mailadress1,
        q_zu_notcenter_mailadress2,
        q_zu_notcenter_schema,
        q_zu_notcenter_in_use,
        q_zu_notcenter_antvarning,
        q_zu_notcenter_execdat,
        q_zu_notcenter_varndat,
        q_zu_notcenter_select2,
        q_zu_notcenter_sysl,
        q_zu_notcenter_startdat,
        q_zu_notcenter_schedule,
        q_zu_notcenter_antal_eskalera,
        q_zu_notcenter_email_eskalera,
        q_zu_notcenter_bcc,
        q_zu_notcenter_cc,
        q_zu_notcenter_dyn_adress,
        q_zu_notcenter_language
    )
    VALUES (
        source.q_zu_notcenter_nr,
        source.perssign,
        source.regdat,
        source.rowcreatedby,
        source.rowcreateddt,
        source.rowupdatedby,
        source.rowupdateddt,
        source.foretagkod,
        source.q_zu_notcenter_beskrivning,
        source.q_zu_notcenter_typ,
        source.q_zu_notcenter_prio,
        source.q_zu_notcenter_varntext,
        source.q_zu_notcenter_kommentar,
        source.q_zu_notcenter_mailadress1,
        source.q_zu_notcenter_mailadress2,
        source.q_zu_notcenter_schema,
        source.q_zu_notcenter_in_use,
        source.q_zu_notcenter_antvarning,
        source.q_zu_notcenter_execdat,
        source.q_zu_notcenter_varndat,
        source.q_zu_notcenter_select2,
        source.q_zu_notcenter_sysl,
        source.q_zu_notcenter_startdat,
        source.q_zu_notcenter_schedule,
        source.q_zu_notcenter_antal_eskalera,
        source.q_zu_notcenter_email_eskalera,
        source.q_zu_notcenter_bcc,
        source.q_zu_notcenter_cc,
        source.q_zu_notcenter_dyn_adress,
        source.q_zu_notcenter_language
    );

IF OBJECT_ID('dbo.stg_q_zu_notcenter_log', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.q_zu_notcenter_log (
        foretagkod,
        q_zu_notcenter_nr,
        regdat,
        q_zu_notcenter_beskrivning,
        q_zu_notcenter_typ,
        q_zu_notcenter_prio,
        q_zu_notcenter_varntext,
        q_zu_notcenter_kommentar,
        q_zu_notcenter_mailadress1,
        q_zu_notcenter_mailadress2,
        q_zu_notcenter_cc,
        q_zu_notcenter_bcc,
        q_zu_notcenter_schema,
        q_zu_notcenter_in_use,
        q_zu_notcenter_antvarning,
        q_zu_notcenter_execdat,
        q_zu_notcenter_varndat,
        q_zu_notcenter_select2,
        q_zu_notcenter_sysl,
        q_zu_notcenter_startdat,
        q_zu_notcenter_schedule,
        q_zu_notcenter_recipients,
        q_zu_notcenter_subject,
        q_zu_notcenter_html
    )
    SELECT
        s.foretagkod,
        s.q_zu_notcenter_nr,
        s.regdat,
        s.q_zu_notcenter_beskrivning,
        s.q_zu_notcenter_typ,
        s.q_zu_notcenter_prio,
        s.q_zu_notcenter_varntext,
        s.q_zu_notcenter_kommentar,
        s.q_zu_notcenter_mailadress1,
        s.q_zu_notcenter_mailadress2,
        s.q_zu_notcenter_cc,
        s.q_zu_notcenter_bcc,
        s.q_zu_notcenter_schema,
        s.q_zu_notcenter_in_use,
        s.q_zu_notcenter_antvarning,
        s.q_zu_notcenter_execdat,
        s.q_zu_notcenter_varndat,
        s.q_zu_notcenter_select2,
        s.q_zu_notcenter_sysl,
        s.q_zu_notcenter_startdat,
        s.q_zu_notcenter_schedule,
        s.q_zu_notcenter_recipients,
        s.q_zu_notcenter_subject,
        s.q_zu_notcenter_html
    FROM dbo.stg_q_zu_notcenter_log s
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.q_zu_notcenter_log t
        WHERE t.foretagkod = s.foretagkod
          AND t.q_zu_notcenter_nr = s.q_zu_notcenter_nr
          AND ISNULL(t.regdat, '19000101') = ISNULL(s.regdat, '19000101')
          AND ISNULL(t.q_zu_notcenter_subject, '') = ISNULL(s.q_zu_notcenter_subject, '')
    );
END
