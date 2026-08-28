# Metrics (Definitions)

## Omsättning (Revenue)
- Definition (generellt): summan av belopp för ordrar i vald period.
- Jeeves-orderbaserad approximation:
  - Inkl moms: SUM(`oh.OrdSumInklMoms`)
  - Exkl moms: SUM(`oh.OrdSumExklMoms`)

## Snittordervärde (AOV)
- AOV = Omsättning / antal ordrar i vald period

## Toppsäljande produkter
- Grupp: `orp.ArtNr`
- Belopp: SUM(`orp.vb_RadVardeInklMoms` eller `orp.vb_RadVardeExklMoms`)
- Antal sålda: SUM(`orp.OrdAntal`)

