SET NOCOUNT ON;
GO

-- Source: Results1.csv -> dbo.q_zu_notcenter_varningstyp
DELETE FROM dbo.q_zu_notcenter_varningstyp;
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (10, N'Artikel');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (15, N'Order');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (20, N'Kund');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (25, N'Kundfaktura');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (30, N'Leverantör');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (35, N'Leverantörsfaktura');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (40, N'Prislista');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (45, N'Inköp');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (50, N'Redovisning');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (55, N'Produktion');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (60, N'Marknad');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (65, N'Projekt,Uppdrag');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (70, N'Service');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (80, N'Övrigt');
INSERT INTO dbo.q_zu_notcenter_varningstyp ([q_zu_notcenter_typ], [q_zu_notcenter_typbeskr]) VALUES (90, N'Integration');
GO

-- Source: Results2.csv -> dbo.q_zu_notcenter_varningskat
DELETE FROM dbo.q_zu_notcenter_varningskat;
INSERT INTO dbo.q_zu_notcenter_varningskat ([q_zu_notcenter_prio], [q_zu_notcenter_typbeskr]) VALUES (10, N'Åtgärda snarast');
INSERT INTO dbo.q_zu_notcenter_varningskat ([q_zu_notcenter_prio], [q_zu_notcenter_typbeskr]) VALUES (20, N'Kontrollera detta');
INSERT INTO dbo.q_zu_notcenter_varningskat ([q_zu_notcenter_prio], [q_zu_notcenter_typbeskr]) VALUES (30, N'Information');
GO

-- Source: Results.csv -> dbo.q_zu_notcenter
DELETE FROM dbo.q_zu_notcenter;
INSERT INTO dbo.q_zu_notcenter ([q_zu_notcenter_nr], [perssign], [regdat], [rowcreatedby], [rowcreateddt], [rowupdatedby], [rowupdateddt], [foretagkod], [q_zu_notcenter_beskrivning], [q_zu_notcenter_typ], [q_zu_notcenter_prio], [q_zu_notcenter_varntext], [q_zu_notcenter_kommentar], [q_zu_notcenter_mailadress1], [q_zu_notcenter_mailadress2], [q_zu_notcenter_schema], [q_zu_notcenter_in_use], [q_zu_notcenter_antvarning], [q_zu_notcenter_execdat], [q_zu_notcenter_varndat], [q_zu_notcenter_select2], [q_zu_notcenter_sysl], [q_zu_notcenter_startdat], [q_zu_notcenter_schedule], [q_zu_notcenter_antal_eskalera], [q_zu_notcenter_email_eskalera], [q_zu_notcenter_bcc], [q_zu_notcenter_cc], [q_zu_notcenter_dyn_adress], [q_zu_notcenter_language]) VALUES (1, N'JIS', N'2026-03-10 00:00:00.000', N'JIS', N'2026-03-10 14:25:00.000', N'TRG', N'2026-03-10 14:27:00.000', 9900, N'Övertidsarbete', 55, 30, N'Övertidsarbete', NULL, NULL, N'Viktor.persson@zeeu.se', 10, 1, NULL, N'2026-03-10 16:00:00.000', NULL, N'SELECT 
    p.perssign2 AS AnstalldID,
    p.RespNamn AS Namn,
    ws.AttRWorkScheduleTemplate AS Grupp,

    -- Faktisk starttid (instämpling)
    CAST(p.komtid AS TIME) AS Faktisk_Starttid,

    -- Schemalagd starttid
    CAST(MIN(r.AttRDayScheduleTime) AS TIME) AS Schemalagd_Starttid,

    -- Schemalagd sluttid
    CAST(MAX(r.AttRDayScheduleTime) AS TIME) AS Schemalagd_Sluttid,

    -- Faktisk utstämpling
    CAST(p.gicktid AS TIME) AS Utstämpling,

    -- Faktiskt arbetade minuter (från faktisk starttid till utstämpling)
    DATEDIFF(MINUTE, p.komtid, p.gicktid) AS Faktiskt_Arbetade_Minuter,

    -- Planerad arbetstid i minuter
    DATEDIFF(MINUTE, MIN(r.AttRDayScheduleTime), MAX(r.AttRDayScheduleTime)) AS Planerad_Arbetstid,

    -- Mer-arbetad tid = faktiskt arbetad tid - planerad arbetstid
    CASE 
        WHEN p.komtid IS NULL OR p.gicktid IS NULL THEN 0
        ELSE DATEDIFF(MINUTE, p.komtid, p.gicktid) - DATEDIFF(MINUTE, MIN(r.AttRDayScheduleTime), MAX(r.AttRDayScheduleTime))
    END AS Mer_Minuter,

    -- Status baserat på övertid
    CASE 
        WHEN p.gicktid IS NULL THEN ''NOT CLOCKED OUT''
        WHEN DATEDIFF(MINUTE, MIN(r.AttRDayScheduleTime), MAX(r.AttRDayScheduleTime)) < DATEDIFF(MINUTE, p.komtid, p.gicktid) THEN ''OVERTIME''
        ELSE ''ON TIME''
    END AS Status

FROM prv p WITH (READUNCOMMITTED)

-- Koppla person till work schedule template
LEFT JOIN AttR_prvws ws WITH (READUNCOMMITTED)
    ON ws.Perssign2 = p.perssign2

-- Koppla till schemarader för personens template
LEFT JOIN AttR_dayschedrow r WITH (READUNCOMMITTED)
    ON r.AttRDaySchedule = ws.AttRWorkScheduleTemplate

WHERE p.gickdatum = CAST(GETDATE() AS DATE)  -- dagens utstämplingar

GROUP BY 
    p.perssign2, p.RespNamn, ws.AttRWorkScheduleTemplate, p.gickdatum, p.gicktid, p.komtid', NULL, N'2026-03-10 00:00:00.000', 10, NULL, NULL, NULL, NULL, NULL, 0);
INSERT INTO dbo.q_zu_notcenter ([q_zu_notcenter_nr], [perssign], [regdat], [rowcreatedby], [rowcreateddt], [rowupdatedby], [rowupdateddt], [foretagkod], [q_zu_notcenter_beskrivning], [q_zu_notcenter_typ], [q_zu_notcenter_prio], [q_zu_notcenter_varntext], [q_zu_notcenter_kommentar], [q_zu_notcenter_mailadress1], [q_zu_notcenter_mailadress2], [q_zu_notcenter_schema], [q_zu_notcenter_in_use], [q_zu_notcenter_antvarning], [q_zu_notcenter_execdat], [q_zu_notcenter_varndat], [q_zu_notcenter_select2], [q_zu_notcenter_sysl], [q_zu_notcenter_startdat], [q_zu_notcenter_schedule], [q_zu_notcenter_antal_eskalera], [q_zu_notcenter_email_eskalera], [q_zu_notcenter_bcc], [q_zu_notcenter_cc], [q_zu_notcenter_dyn_adress], [q_zu_notcenter_language]) VALUES (1, N'JIS', N'2026-03-10 00:00:00.000', N'JIS', N'2026-03-10 15:03:00.000', N'TRG', NULL, 9930, N'Övertidsarbete', 80, 30, N'Övertidsarbete', NULL, NULL, N'Viktor.persson@zeeu.se', 40, 1, NULL, N'2026-03-10 16:00:00.000', NULL, N'SELECT 
    p.perssign2 AS AnstalldID,
    p.RespNamn AS Namn,
    ws.AttRWorkScheduleTemplate AS Grupp,

    -- Faktisk starttid (instämpling)
    CAST(p.komtid AS TIME) AS Faktisk_Starttid,

    -- Schemalagd starttid
    CAST(MIN(r.AttRDayScheduleTime) AS TIME) AS Schemalagd_Starttid,

    -- Schemalagd sluttid
    CAST(MAX(r.AttRDayScheduleTime) AS TIME) AS Schemalagd_Sluttid,

    -- Faktisk utstämpling
    CAST(p.gicktid AS TIME) AS Utstämpling,

    -- Faktiskt arbetade minuter (från faktisk starttid till utstämpling)
    DATEDIFF(MINUTE, p.komtid, p.gicktid) AS Faktiskt_Arbetade_Minuter,

    -- Planerad arbetstid i minuter
    DATEDIFF(MINUTE, MIN(r.AttRDayScheduleTime), MAX(r.AttRDayScheduleTime)) AS Planerad_Arbetstid,

    -- Mer-arbetad tid = faktiskt arbetad tid - planerad arbetstid
    CASE 
        WHEN p.komtid IS NULL OR p.gicktid IS NULL THEN 0
        ELSE DATEDIFF(MINUTE, p.komtid, p.gicktid) - DATEDIFF(MINUTE, MIN(r.AttRDayScheduleTime), MAX(r.AttRDayScheduleTime))
    END AS Mer_Minuter,

    -- Status baserat på övertid
    CASE 
        WHEN p.gicktid IS NULL THEN ''NOT CLOCKED OUT''
        WHEN DATEDIFF(MINUTE, MIN(r.AttRDayScheduleTime), MAX(r.AttRDayScheduleTime)) < DATEDIFF(MINUTE, p.komtid, p.gicktid) THEN ''OVERTIME''
        ELSE ''ON TIME''
    END AS Status

FROM prv p WITH (READUNCOMMITTED)

-- Koppla person till work schedule template
LEFT JOIN AttR_prvws ws WITH (READUNCOMMITTED)
    ON ws.Perssign2 = p.perssign2

-- Koppla till schemarader för personens template
LEFT JOIN AttR_dayschedrow r WITH (READUNCOMMITTED)
    ON r.AttRDaySchedule = ws.AttRWorkScheduleTemplate

WHERE p.gickdatum = CAST(GETDATE() AS DATE)  -- dagens utstämplingar

GROUP BY 
    p.perssign2, p.RespNamn, ws.AttRWorkScheduleTemplate, p.gickdatum, p.gicktid, p.komtid', NULL, N'2026-03-10 00:00:00.000', 10, NULL, NULL, NULL, NULL, NULL, 0);
GO

