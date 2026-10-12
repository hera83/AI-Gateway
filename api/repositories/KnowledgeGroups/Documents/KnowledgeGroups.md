# KnowledgeGroups (repository)

**Formål:** Opretter, henter og sletter en API-nøgles navngivne knowledge base-grupper (fx "Kunde data").
**Brug det når:** opgaven handler om grupperne selv.
**Brug det ikke til:** dokumenter, chunks eller vektorer (det er `KnowledgeDocuments`) eller til at slette en gruppe med indhold (brug `IKnowledgeBaseManager.DeleteGroupAsync`).
**Tabeller/filer:** `KnowledgeGroups`

## Metoder
| Metode | Beskrivelse |
|---|---|
| `CreateAsync(CreateKnowledgeGroupDto dto)` | Opretter gruppen. `null` hvis nøglen allerede har en gruppe med samme navn. |
| `GetAllAsync(Guid apiKeyId)` | Nøglens grupper sorteret efter navn. |
| `GetByIdAsync(Guid apiKeyId, Guid id)` | Én af nøglens grupper, eller `null`. |
| `DeleteAsync(Guid apiKeyId, Guid id)` | Sletter gruppen. Databasen kaskaderer til dokument- og chunk-rækker. `false` hvis den ikke findes. |

## Faldgruber
- Alle metoder filtrerer på `apiKeyId`. En anden nøgles gruppe svarer som "ikke fundet" (404), ikke 403, så der lækkes intet.
- `DeleteAsync` alene efterlader vektorer i `vectors.db` og filer i `app_files/`. Kald den kun via manageren.
