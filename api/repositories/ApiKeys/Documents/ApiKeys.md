# ApiKeys (repository)

**Formål:** Opretter, henter, opdaterer, roterer, aktiverer/deaktiverer og sletter API-nøgler, og fører audit-log over hver ændring.
**Brug det når:** opgaven handler om standard-nøgler i `app.db`, også opslag ved authentication.
**Brug det ikke til:** master-nøglen (den står kun i `ApiKeys:MasterKey` og har ingen række) eller til at generere/hashe nøgler (det gør `infrastructure/Authentication/ApiKeyGenerator`).
**Tabeller/filer:** `ApiKeys` + `ApiKeyAuditLogs`

## Metoder
| Metode | Beskrivelse |
|---|---|
| `CreateAsync(CreateApiKeyDto dto)` | Opretter en aktiv nøgle ud fra hash og preview og skriver en `Created`-audit-række. |
| `GetAllAsync()` | Alle nøgler sorteret efter navn. |
| `GetByIdAsync(Guid id)` | Én nøgle, eller `null`. |
| `GetValidByHashAsync(string keyHash)` | Nøglen med dette SHA-256-hash, hvis den er aktiv og ikke udløbet, ellers `null`. Bruges af `ApiKeyAuthenticationHandler`. |
| `UpdateAsync(Guid id, UpdateApiKeyDto dto)` | Opdaterer navn, ansvarlig, kontaktinfo og udløb. `null` hvis den ikke findes. |
| `RolloverAsync(Guid id, string keyHash, string keyPreview)` | Giver nøglen nyt hash og preview, sætter `RolledOverAt` og forlænger `ExpiresAt` et år. Den gamle nøgle holder op med at virke med det samme. `null` hvis den ikke findes. |
| `SetActiveAsync(Guid id, bool isActive)` | Aktiverer (udløb +1 år) eller deaktiverer (udløb = nu). `null` hvis den ikke findes. |
| `GetAuditLogAsync(Guid id)` | Nøglens audit-log, nyeste først. `null` hvis nøglen ikke findes. |
| `MarkUsedAsync(Guid id)` | Sætter `LastUsedAt` direkte i databasen. Kaldes ved hvert autentificeret request. |
| `DeleteAsync(Guid id)` | Hard delete. Databasen kaskaderer til audit-log og knowledge base-rækker. `false` hvis den ikke findes. |

## Faldgruber
- Audit-rækken skrives i samme `SaveChangesAsync` som ændringen, så de altid følges ad.
- `DeleteAsync` rydder ikke vektorer og filer op. Brug `IApiKeyManager.DeleteAsync`, som sletter nøglens knowledge base-dokumenter først.
