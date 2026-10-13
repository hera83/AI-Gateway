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
| `GetNextProcessingAsync()` | Det ældste dokument med status `Processing` på tværs af alle nøgler, eller `null`. Bruges af baggrundsbehandlingen (`KnowledgeDocumentProcessingWorker` via manageren). |
| `GetNextWithOutdatedMarkdownAsync(int currentVersion)` | Det ældste `Indexed` dokument uden Markdown eller med `MarkdownVersion` under `currentVersion`, på tværs af alle nøgler, eller `null`. Bruges af baggrundsbehandlingen. |
| `GetNextMarkdownLlmPendingAsync(int currentVersion)` | Det ældste `Indexed` dokument med `MarkdownSource = LlmPending` og Markdown i den aktuelle version, med strukturens Markdown og en evt. påbegyndt kladde, eller `null`. Bruges af `KnowledgeMarkdownWorker`. |
| `GetMarkdownAsync(Guid apiKeyId, Guid id)` | Status, fejlbesked, Markdown-versionen (`MarkdownText`), hvordan den er lavet (`MarkdownSource`; `null` vises som `Structure`) og LLM-fremskridt (`LlmPartsDone`/`LlmPartsTotal`). `null` hvis dokumentet ikke findes. |
| `SetMarkdownAsync(Guid id, string markdown, bool needsLlm, int version)` | Gemmer kun Markdown-versionen med kilden `LlmPending` eller `Structure` og converter-versionen. Tekst, chunks og vektorer røres ikke. `false` hvis dokumentet ikke findes. |
| `SaveLlmMarkdownProgressAsync(Guid id, string draft, int partsDone, int partsTotal)` | Gemmer LLM-kladden efter hver del, så en genstart fortsætter derfra. |
| `SetLlmMarkdownAsync(Guid id, string markdown)` | Gemmer LLM'ens Markdown med kilden `Llm` og rydder kladden. |
| `SkipLlmMarkdownAsync(Guid id)` | Beholder den nuværende Markdown, sætter kilden til `Structure` og tager dokumentet ud af LLM-køen. |
| `GetTextAsync(Guid apiKeyId, Guid id)` | Status, fejlbesked og den udtrukne tekst (`null` indtil status er `Indexed`). `null` hvis dokumentet ikke findes. |
| `GetFileAsync(Guid apiKeyId, Guid id)` | Absolut sti, originalt filnavn og content type til download. `null` hvis rækken eller filen mangler. |
| `OpenFileAsync(Guid apiKeyId, Guid id)` | Åbner den gemte fil til læsning med `FileShare.Read | FileShare.Delete`, så dokumentet kan slettes, mens filen læses. `null` hvis rækken eller filen mangler. |
| `DeleteOrphanedFilesAsync(TimeSpan minAge)` | Sletter filer i `app_files/KnowledgeDocuments/`, som ingen række peger på, og som er ældre end `minAge`. Returnerer antallet. Bruges af `KnowledgeFileCleanupWorker`. |
| `MarkFailedAsync(Guid id, string errorMessage)` | Sætter status `Failed` med fejlbesked. Rydder først change trackeren, så et fejlet `MarkIndexedAsync` i samme scope ikke forsøges gemt igen. |
| `ReplaceChunksAsync(Guid id, List<CreateKnowledgeChunkDto> chunks, int indexVersion)` | Indekserer et `Indexed` dokument igen: indsætter nye vektorer og chunk-rækker og fjerner de gamle i samme `SaveChangesAsync` (de gamle vektorer slettes bagefter), så en søgning aldrig finder dokumentet uden chunks. Sætter `IndexVersion`. `false` hvis dokumentet ikke findes. |
| `GetNextWithOutdatedIndexAsync(int currentVersion)` | Det ældste `Indexed` dokument med `IndexVersion` under `currentVersion` og med Markdown, men ikke i `LlmPending` (LLM-workeren indekserer dem, når den er færdig), på tværs af alle nøgler, eller `null`. |
| `MarkIndexedAsync(Guid id, IndexKnowledgeDocumentDto dto)` | Indsætter vektorer i `vec_chunks` og chunk-rækker, gemmer den udtrukne tekst og Markdown-versionen (kilde `LlmPending` eller `Structure` efter `MarkdownNeedsLlm`) og sætter status `Indexed`. |
| `SearchAsync(Guid apiKeyId, List<double> embedding, int topK, Guid? knowledgeGroupId)` | KNN-søgning på tværs af alle nøglens dokumenter, eller kun i én gruppe, når `knowledgeGroupId` er sat. Gruppefilteret ligger i selve KNN-forespørgslen (`group_id`), så der kommer op til `topK` træf fra gruppen. `Score` er 1 - cosine distance. Tjekker ikke, at gruppen findes. |
| `DeleteAsync(Guid apiKeyId, Guid id)` | Sletter rækken (kaskade til chunks), vektorerne og filen. `false` hvis den ikke findes. |

## Faldgruber
- `vec_chunks` er en sqlite-vec `vec0`-virtual table, oprettet med rå SQL i migrationen `AddVectorChunksTable`. `api_key_id` er en `partition key`, og `group_id` er en metadata-kolonne. Brug aldrig `+`-præfikset (auxiliary columns), for de kan ikke bruges i en KNN-forespørgsels `WHERE`.
- `KnowledgeChunk.VectorRowId` er rowid'en i `vec_chunks` og binder de to databaser sammen.
- Skiftes `KnowledgeBase:EmbeddingModel`/`EmbeddingDimensions`, skal `vec_chunks` genskabes med en ny migration (`float[768]` står i SQL'en), og alle dokumenter skal embeddes igen.
- Alle Markdown-skrivninger undtagen `SaveLlmMarkdownProgressAsync` rydder LLM-kladden (`MarkdownLlmDraft`, `MarkdownLlmPartsDone`, `MarkdownLlmPartsTotal`).
- Filsletning er best-effort og logger en advarsel ved `IOException`/`UnauthorizedAccessException` (OneDrive eller en igangværende download kan låse filen). Rækken er altid slettet først, og `KnowledgeFileCleanupWorker` fjerner filen senere (`DeleteOrphanedFilesAsync`).
- Åbn aldrig en gemt fil uden `FileShare.Delete`. Workeren læser filer i minutter (OCR), og på Windows afviser `File.Delete` ellers sletningen, så filen bliver liggende uden række.
- `DeleteOrphanedFilesAsync` rører kun filer ældre end `minAge`, fordi `CreateAsync` skriver filen lige før rækken.
