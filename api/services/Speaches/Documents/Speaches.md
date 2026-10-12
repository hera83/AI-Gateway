# Speaches (service)

**Formål:** Taler med Speaches (OpenAI-kompatibel tale-til-tekst og tekst-til-tale): chat completions, transskribering (også realtime over WebSocket), oversættelse, talesyntese, speaker-embeddings, VAD-tidsstempler, diarization og model-administration.
**Brug det når:** noget skal arbejde med lyd eller Speaches' modeller.
**Brug det ikke til:** at gemme lyd eller resultater. Servicen rører aldrig `app.db`, og syntetiseret lyd sendes direkte videre til klienten.
**Eksternt system og konfiguration:** Speaches (`/v1/*` og `/api/ps`). Sektion `Speaches` i `appsettings.json`: `BaseUrl` (standard `http://localhost:8000`) og `TimeoutMinutes` (standard 75, da lange transskriberinger kan tage op til en time). Full pattern: `SpeachesConfigurationProvider` → `SpeachesHttpClientFactory` → `SpeachesService`.

## Metoder
| Metode | Endpoint | Beskrivelse |
|---|---|---|
| `ChatCompletionsAsync` | `POST v1/chat/completions` | OpenAI-kompatibel chat (ikke-streaming). |
| `TranscribeAsync` | `POST v1/audio/transcriptions` | Transskriberer en lydfil (multipart, `verbose_json`). |
| `ProxyRealtimeTranscriptionAsync` | `WS v1/realtime?intent=transcription` | Duplex relay mellem klientens WebSocket og Speaches. Forbinder til Speaches først og accepterer først derefter klienten (via delegaten `acceptClientAsync`), så en utilgængelig Speaches giver 502 i stedet for en afbrudt socket. |
| `TranslateAsync` | `POST v1/audio/translations` | Oversætter tale til engelsk tekst. |
| `SynthesizeSpeechAsync` | `POST v1/audio/speech` | Tekst til lyd. Returnerer bytes + content type (`audio/mp3` rettes til `audio/mpeg`). |
| `CreateSpeechEmbeddingAsync` | `POST v1/audio/speech/embedding` | Speaker-embedding af en lydfil. |
| `DetectSpeechTimestampsAsync` | `POST v1/audio/speech/timestamps` | VAD: hvor i lyden der tales. |
| `DiarizeAsync` | `POST v1/audio/diarization` | Hvem taler hvornår. |
| `ListModelsAsync` / `GetRegistryAsync` | `GET v1/models` / `v1/registry` | Installerede/tilgængelige modeller, evt. filtreret på `task`. |
| `ListAudioModelsAsync` / `ListVoicesAsync` | `GET v1/audio/models` / `v1/audio/voices` | Lydmodeller og stemmer. |
| `GetModelAsync` / `DownloadModelAsync` / `DeleteModelAsync` | `GET/POST/DELETE v1/models/{id}` | Én model. |
| `ListRunningModelsAsync` / `LoadModelAsync` / `StopModelAsync` | `GET/POST/DELETE api/ps[/{id}]` | Modeller i hukommelsen. |

## Faldgruber
- Wire-formatet er snake_case via `JsonOptions` (`SnakeCaseLower`). `null`-felter udelades (`WhenWritingNull`), fordi Speaches afviser `null` for felter med en standardværdi (fx `speed` på `v1/audio/speech` giver 422).
- `POST v1/models/{id}` svarer med ren tekst (`Model '...' downloaded`) og ikke JSON. Download og sletning læser derfor ikke svaret.
- Fejlbeskeder (`SpeachesException.Message`) indeholder kun stien og aldrig `BaseUrl`'s vært, fordi de sendes til klienten som `detail`.
- `v1/audio/voices` svarer `{ "voices": [...] }` (id, name, language, evt. gender) og ikke `ListModelsResponse`, som Speaches' egen OpenAPI-spec påstår. Derfor har `ListVoicesAsync` sin egen `ListVoicesResponseDto`.
- Fejl giver `SpeachesException` med Speaches' statuskode og `detail`/`error`-tekst. `GlobalExceptionHandler` sender statuskoden videre.
- Multipart-kald disposer indholdet (og dermed upload-streamen), når kaldet er færdigt.
- I realtime-relayet styres hver retning kun af requestets eget token. Lad være med at annullere den anden retning, når den ene fejler (se kommentaren i `SpeachesService.Realtime.cs`).
