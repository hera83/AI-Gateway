# KnowledgeBase (manager)

**Formål:** Giver hver standard-nøgle sin egen private vidensbank til RAG: upload med tekstudtræk, chunking og embedding, sletning af grupper med indhold, vektorsøgning og RAG-chat.
**Brug det når:** en handling kombinerer knowledge base-repositories med Ollama, eller sletter en gruppe med dokumenter.
**Brug det ikke til:** simple opslag (`ListGroups`, `ListDocuments`, `DownloadDocument`, `CreateGroup`, `DeleteDocument`). De kalder repositoriet direkte (hybrid-reglen).
**Afhænger af:** `IKnowledgeGroupRepository`, `IKnowledgeDocumentRepository`, `IOllamaService` og `KnowledgeBaseSettings` (sektion `KnowledgeBase` i `appsettings.json`).

## Metoder
| Metode | Beskrivelse |
|---|---|
| `UploadDocumentAsync(UploadDocumentDto dto)` | Tjekker gruppe, størrelse og filtype (`.txt`, `.md`, `.pdf`, `.docx`), gemmer filen, udtrækker tekst, chunker, embedder med `KnowledgeBase:EmbeddingModel` og indekserer. Udfaldet er `UploadDocumentOutcome`. Fejler udtrækket, er dokumentet stadig gemt med status `Failed`. |
| `DeleteGroupAsync(Guid apiKeyId, Guid knowledgeGroupId)` | Sletter gruppens dokumenter (rækker, vektorer og filer) og derefter gruppen. `false` hvis gruppen ikke findes. |
| `SearchAsync(Guid apiKeyId, string query, int? topK)` | Embedder forespørgslen og finder de mest relevante chunks på tværs af alle nøglens grupper. `topK` er som standard `KnowledgeBase:DefaultTopK`. |
| `RagChatAsync(RagChatDto dto)` | Søger på den seneste `user`-besked, bygger en ny system-prompt med de fundne chunks (erstatter en evt. medsendt system-besked) og chatter via Ollama. Returnerer svaret plus `Sources`. |

## Faldgruber
- Master-nøglen har ingen `ApiKeys`-række og kan ikke eje data. Controlleren afviser den med 403, før manageren kaldes.
- Upload og indeksering sker synkront i requestet. Der er ingen jobkø.
- `DocumentTextExtractor` ligger her og ikke i `extensions/`, fordi kun knowledge base bruger den.
- Skiftes embedding-model eller -dimension, skal `vec_chunks` genskabes, og alle dokumenter skal embeddes igen.
