# Logs (repository)

**Formål:** Søger og filtrerer i applikationens egne logs.
**Brug det når:** opgaven handler om at læse logs (fx `GET /Logs/Search`).
**Brug det ikke til:** at skrive logs (brug `ILogger<T>`) eller til at rydde op (det gør `LogCleanupWorker`).
**Tabeller/filer:** tabellen `Logs` i `app_dbs/log.db` (ejes af Serilog.Sinks.SQLite)

## Metoder
| Metode | Beskrivelse |
|---|---|
| `SearchAsync(SearchLogsDto query)` | Filtrerer på level, delstreng i besked/exception og UTC-tidsinterval, nyeste først, med paging (højst 500 pr. side). |

## Faldgruber
- Read-only ADO.NET uden EF. Skemaet ejes af sinken, så kolonnenavnene står i `LogMapper.Columns`.
- Stien kommer fra `LogDatabaseSettings`, så sink, repository og oprydning altid rammer samme fil.
- Tidsstempler gemmes i UTC i formatet `yyyy-MM-ddTHH:mm:ss.fff`, og filtrene sammenligner strenge i samme format.
