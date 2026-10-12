# KnowledgeDocuments (repository)

**Formål:** Gemmer, henter og sletter knowledge base-dokumenter med deres originale fil, tekst-chunks og embedding-vektorer, og laver vektorsøgning.
**Brug det når:** opgaven handler om dokumenter, chunks, vektorer eller de uploadede filer.
**Brug det ikke til:** tekstudtræk, chunking eller embeddings (det gør `IKnowledgeBaseManager` med `IOllamaService`), eller til grupper (`KnowledgeGroups`).
**Tabeller/filer:** `KnowledgeDocuments` + `KnowledgeChunks` (`app.db`), `vec_chunks` (`vectors.db`, attached som `vectors`) + filer i `app_files/KnowledgeDocuments/`

## Metoder
| Metode | Beskrivelse |
|---|---|
| `CreateAsync(CreateKnowledgeDocumentDto dto)` | Skriver filen som `<guid>.<endelse>` og opretter rækken med status `Processing`. Fejler databasen, slettes filen igen. |
| `GetAllAsync(Guid apiKeyId, Guid? knowledgeGroupId)` | Nøglens dokumenter, evt. kun i én gruppe, nyeste først. |
| `GetIdsAsync(Guid apiKeyId, Guid? knowledgeGroupId)` | Kun id'erne. Bruges af managers til at slette alt i en gruppe eller under en nøgle. |
| `GetFileAsync(Guid apiKeyId, Guid id)` | Absolut sti, originalt filnavn og content type til download. `null` hvis rækken eller filen mangler. |
| `OpenFileAsync(Guid apiKeyId, Guid id)` | Åbner den gemte fil til læsning. `null` hvis rækken eller filen mangler. |
| `MarkFailedAsync(Guid id, string errorMessage)` | Sætter status `Failed` med fejlbesked. |
| `MarkIndexedAsync(Guid id, IndexKnowledgeDocumentDto dto)` | Indsætter vektorer i `vec_chunks` og chunk-rækker, gemmer den udtrukne tekst og sætter status `Indexed`. |
| `SearchAsync(Guid apiKeyId, List<double> embedding, int topK)` | KNN-søgning på tværs af alle nøglens dokumenter. `Score` er 1 - cosine distance. |
| `DeleteAsync(Guid apiKeyId, Guid id)` | Sletter rækken (kaskade til chunks), vektorerne og filen. `false` hvis den ikke findes. |

## Faldgruber
- `vec_chunks` er en sqlite-vec `vec0`-virtual table, oprettet med rå SQL i migrationen `AddVectorChunksTable`. `api_key_id` er en `partition key`, og `group_id` er en metadata-kolonne. Brug aldrig `+`-præfikset (auxiliary columns), for de kan ikke bruges i en KNN-forespørgsels `WHERE`.
- `KnowledgeChunk.VectorRowId` er rowid'en i `vec_chunks` og binder de to databaser sammen.
- Skiftes `KnowledgeBase:EmbeddingModel`/`EmbeddingDimensions`, skal `vec_chunks` genskabes med en ny migration (`float[768]` står i SQL'en), og alle dokumenter skal embeddes igen.
- Filsletning er best-effort og logger en advarsel ved `IOException`/`UnauthorizedAccessException` (OneDrive kan låse filer kortvarigt). Rækken er altid slettet først.
