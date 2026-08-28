/*
Exportera gamla NotifyMe-data från Jeeves-databasen.

Körs i källdatabasen (Jeeves). Resultaten kan sedan importeras till portalens Azure DB
via "Results to Grid" -> export till CSV/Excel eller via SSMS import wizard.

Tabeller:
- dbo.q_zu_notcenter
- dbo.q_zu_notcenter_varningstyp
- dbo.q_zu_notcenter_varningskat
- dbo.q_zu_notcenter_log
*/

SET NOCOUNT ON;

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
FROM dbo.q_zu_notcenter
ORDER BY foretagkod, q_zu_notcenter_nr;

SELECT
    q_zu_notcenter_typ,
    q_zu_notcenter_typbeskr
FROM dbo.q_zu_notcenter_varningstyp
ORDER BY q_zu_notcenter_typ;

SELECT
    q_zu_notcenter_prio,
    q_zu_notcenter_typbeskr
FROM dbo.q_zu_notcenter_varningskat
ORDER BY q_zu_notcenter_prio;

SELECT
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
FROM dbo.q_zu_notcenter_log
ORDER BY regdat DESC, q_zu_notcenter_nr DESC;
