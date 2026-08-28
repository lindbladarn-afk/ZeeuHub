# Jeeves Joins (Join Hints)

## Orders -> Order lines
- `dbo.oh.OrderNr = dbo.orp.OrderNr`
- Recommended: also align on `ForetagKod` when possible (depends on availability in both tables)

## Orders -> Customers
- `dbo.oh.ForetagKod = dbo.fr.ForetagKod`
- `dbo.oh.FtgNr = dbo.fr.FtgNr`

## Invoices -> Customers
- `dbo.ft.ForetagKod = dbo.fr.ForetagKod`
- `dbo.ft.FtgNr = dbo.fr.FtgNr`

## Notes
- Alla relationer finns inte alltid som FK i databasen.
- Därför är dessa join-regler explicita och bör följas av AI.
