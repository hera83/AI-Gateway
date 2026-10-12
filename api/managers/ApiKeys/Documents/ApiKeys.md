# ApiKeys (manager)

**Formål:** Sletter en API-nøgle helt, inklusive alt den ejer i knowledge base.
**Brug det når:** en nøgle skal slettes permanent (`DELETE /Keys/Delete/{id}`).
**Brug det ikke til:** andre nøgle-handlinger. Opret, hent, opdatér, rollover og (de)aktivering kalder `IApiKeyRepository` direkte (hybrid-reglen).
**Afhænger af:** `IApiKeyRepository`, `IKnowledgeDocumentRepository`.

## Metoder
| Metode | Beskrivelse |
|---|---|
| `DeleteAsync(Guid id)` | Sletter nøglens dokumenter (rækker, vektorer og filer) og derefter nøglen. Databasen kaskaderer resten (audit-log, grupper, chunks). `false` hvis nøglen ikke findes. |

## Faldgruber
- Ikke atomart: fejler en sletning midtvejs, er de allerede slettede dokumenter væk, men nøglen findes stadig. Et nyt kald fortsætter, hvor det slap.
