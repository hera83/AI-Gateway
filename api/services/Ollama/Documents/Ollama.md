# Ollama (service)

**Formål:** Taler med Ollama (LLM-inferens): generate, chat, embeddings, model-administration og version.
**Brug det når:** noget skal generere tekst, chatte eller lave embeddings med en lokal model.
**Brug det ikke til:** at gemme noget. Servicen rører aldrig `app.db`. Knowledge base-embeddings gemmes af `KnowledgeDocuments`-repositoriet via `IKnowledgeBaseManager`.
**Eksternt system og konfiguration:** Ollama REST API (`/api/*`). Sektion `Ollama` i `appsettings.json`: `BaseUrl` (standard `http://localhost:11434`) og `TimeoutMinutes` (standard 15). Full pattern: `OllamaConfigurationProvider` → `OllamaHttpClientFactory` → `OllamaService`. Postman-samling i `Documents/Ollama REST API.postman_collection.json`.

## Metoder
| Metode | Endpoint | Beskrivelse |
|---|---|---|
| `GenerateAsync` / `GenerateStreamAsync` | `POST api/generate` | Tekst ud fra en prompt. Stream-varianten læser NDJSON linje for linje. |
| `ChatAsync` / `ChatStreamAsync` | `POST api/chat` | Chat ud fra en beskedliste, evt. med tools. |
| `EmbedAsync` | `POST api/embed` | Embeddings for én eller flere tekster. |
| `ListModelsAsync` | `GET api/tags` | Lokalt installerede modeller. |
| `ListRunningModelsAsync` | `GET api/ps` | Modeller i hukommelsen. |
| `ShowModelAsync` | `POST api/show` | Detaljer om en model. |
| `CopyModelAsync` | `POST api/copy` | Kopierer en model under nyt navn. |
| `DeleteModelAsync` | `DELETE api/delete` | Sletter en model. |
| `PullModelAsync` / `PushModelAsync` | `POST api/pull` / `api/push` | Henter/sender en model (ikke-streaming). |
| `CreateModelAsync` | `POST api/create` | Opretter en model. |
| `GetVersionAsync` | `GET api/version` | Ollamas version. |

## Faldgruber
- Wire-formatet er snake_case via `JsonOptions` (`SnakeCaseLower`). `[JsonPropertyName]` bruges kun, hvor et navn afviger.
- Streaming kræver `HttpCompletionOption.ResponseHeadersRead`, ellers bufres hele svaret, før første chunk returneres.
- Fejl fra Ollama giver `OllamaException` med Ollamas statuskode og `error`-tekst. `GlobalExceptionHandler` sender statuskoden videre. Kan Ollama ikke nås, giver det `HttpRequestException` (502) eller timeout (504).
- `Stream` sættes af servicen selv. Kalderen vælger metode (`GenerateAsync` eller `GenerateStreamAsync`).
