# Testrapport: AI Gateway

**Dato:** 12. oktober 2026
**Testet version:** commit `52b7590` plus de ikke-committede ændringer i arbejdskopien (OCR i knowledge base m.m.) og rettelserne fra denne test
**Omfang:** hele løsningen `AiGateway.slnx` (ét projekt: `api`), alle API-endpoints og alle eksterne services (Ollama, Speaches, Tesseract)

## 1. Resumé

Alle 50 endpoints i API'ets OpenAPI-dokument er testet end-to-end mod det kørende API, og det samme gælder de tre eksterne services, som gatewayen bruger. Der er kørt 146 tests:

| Resultat | Antal |
|---|---:|
| ✅ Bestået | 136 |
| ❌ Fejlet | 0 |
| ⛔ Blokeret af miljøet (kunne ikke verificeres) | 10 |

Testen fandt **7 fejl i gatewayen**, og alle er rettet:

1. `Speaches/ListVoices` returnerede altid en tom liste.
2. `Speaches/SynthesizeSpeech` fejlede med 422, når `speed` ikke var angivet.
3. `Speaches/DownloadModel` svarede 500, selvom modellen blev hentet.
4. Et upload, hvor embedding fejlede, blev hængende i status `Processing` for altid.
5. Fejlsvar afslørede Ollamas og Speaches' interne IP-adresser og porte for klienterne, også i Production.
6. Realtime-transskription: kunne Speaches ikke nås, blev klientens WebSocket afbrudt uden forklaring.
7. Realtime-transskription: når klienten lukkede forbindelsen, fik den aldrig en kvittering for lukningen.

Rettelse 1-6 er verificeret ved genkørsel. Rettelse 7 kunne ikke verificeres, fordi Speaches gik i stå undervejs (se afsnit 5).

**Det skal gøres nu:** Speaches-serveren skal genstartes. Under testen gik dens tale-til-tekst og model-load/unload i stå, og det gælder også ved direkte kald uden om gatewayen. Talesyntese virker stadig. Efter genstarten bør de 7 Speaches-tests, der er blokeret af det, køres igen.

## 2. Testmiljø

| | |
|---|---|
| Maskine (API) | Windows 10 Pro, Intel Core i7-6600U, 32 GB RAM |
| .NET SDK | 10.0.301. API'et kørt med `dotnet run --project api --launch-profile http` (Development) |
| Testklient | Node.js 24.11.1 (`fetch`, `FormData`, `WebSocket`) |
| Ollama | 0.40.0 på en separat server. Modeller: `qwen2.5:14b-instruct`, `qwen2.5-coder:14b`, `nomic-embed-text`, `gemma4:12b` |
| Speaches | v0.8.2 på en separat server. STT: `faster-whisper` small/medium/large-v3. TTS: piper `da_DK-talesyntese-medium` og `en_US-arctic-medium` |
| Tesseract | 5.4.0 (Windows, `services/Tesseract/Engine/win-x64`) med `dan`, `eng` og `osd` |
| Databaser | Tomme ved start (0 nøgler, 0 dokumenter) og tomme igen efter testen |

## 3. Metode

1. **Build:** `dotnet build` giver 0 fejl og 0 advarsler, både før og efter rettelserne.
2. **End-to-end-test:** et testscript kalder API'et over HTTP og WebSocket præcis som en rigtig klient. Master-nøglen læses fra `appsettings.Development.json` og bliver aldrig skrevet ud. Standard-nøgler oprettes via `Keys/Create`.
3. **Kontrol i databasen:** read-only opslag i `app.db`, `vectors.db` og `log.db`, plus filsystemet under `app_files/`. Sådan er det verificeret, at OCR-teksten er korrekt, og at sletninger fjerner rækker, chunks, vektorer og filer.
4. **Testdokumenter:** `.txt`, `.md`, `.docx`, en PDF med tekstlag, en scannet PDF uden tekstlag (A4 ved 200 dpi), en blandet PDF (tekstlag + et billede med tekst), en `.exe`, en tom fil, en fil på 21 MB og en ødelagt PDF/docx. Al dansk tekst indeholder æ, ø og å. Lyd blev lavet med Speaches' egen talesyntese.
5. **Fejlscenarier:** to ekstra instanser af den byggede `api.dll` på port 5299:
   - **F1:** `Ollama:BaseUrl` og `Speaches:BaseUrl` peger på en lukket port.
   - **F2:** miljøet er `Production`, og `Tesseract:ExecutablePath` peger på en fil, der ikke findes.
6. **Publish:** `dotnet publish -c Release` (samme trin som Dockerfilens build-stage) for at tjekke indholdet af det, der kommer i Docker-imaget.
7. **Forløb:** kørsel 1 (131 tests: 115 bestået, 16 fejlet) → fejlene analyseret og rettet → genopbygning → kørsel 2 + fejlscenarier (146 tests i alt).
8. **Oprydning:** alle testnøgler er slettet via `Keys/Delete`, som også fjerner deres grupper, dokumenter, vektorer og filer. Ollama-modellerne `gw-test-copy` og `gw-test-create` er oprettet og slettet igen. Speaches-modellen `piper-ar_JO-kareem-low` er hentet og slettet igen.

### Dækning

| Del | Dækket af |
|---|---|
| Controllere | Alle 6 controllere og alle 50 actions, både succes og fejlstier (validering, 401/403/404/409/413, upstream-fejl) |
| Ollama-service | Alle 12 metoder, inkl. streaming (NDJSON), tool calling og struktureret output |
| Speaches-service | Alle 18 metoder, inkl. realtime-relayet over WebSocket (se afsnit 5 for det, der er blokeret) |
| Tesseract-service | Via upload: OCR af en hel scannet side, OCR af et billede på en side med tekstlag, og et manglende Tesseract-program |
| Repositories og managers | Gennem endpoints, med kontrol af databaser og filer |
| Authentication | Manglende og ugyldig nøgle, master/standard-policies, deaktivering, rollover og isolation mellem nøgler |
| `LogCleanupWorker` | Kun at den starter uden fejl (grænsen på 1 GiB nås ikke i en test) |

## 4. Fundne og rettede fejl

### Fejl 1: `ListVoices` returnerede altid en tom liste
- **Symptom:** `GET /Speaches/ListVoices` gav `{"data":[],"object":"list"}`, selvom Speaches har stemmerne `talesyntese` og `arctic`.
- **Årsag:** Speaches svarer `{"voices":[...]}`, men gatewayen læste feltet `data`. Speaches' egen OpenAPI-spec beskriver svaret forkert.
- **Rettelse:** nye DTO'er til stemmer. Svaret er nu `{"voices":[{"id","name","language","gender"}],"object":"list"}`, så det spejler Speaches som de andre proxy-lister.
- **Bemærk:** feltnavnet i svaret ændres fra `data` til `voices`. Det gamle svar var altid tomt, så ingen klient kan have brugt det.
- **Filer:** `services/Speaches/Dtos/Models/ListVoicesResponseDto.cs` og `VoiceDto.cs` (nye), `dtos/Speaches/VoiceDto.cs` (ny), `dtos/Speaches/ListVoicesSpeachesResponseDto.cs`, `dtos/Speaches/SpeachesMapper.cs`, `services/Speaches/SpeachesService.Models.cs`, `services/Speaches/Interfaces/ISpeachesService.cs`
- **Verificeret:** ja (kørsel 2).

### Fejl 2: `SynthesizeSpeech` fejlede med 422, når `speed` ikke var angivet
- **Symptom:** alle kald uden `speed` gav 422 `Input should be a valid number`, uanset format. Kun kald med en eksplicit `speed` virkede.
- **Årsag:** gatewayen sendte `"speed": null`, og Speaches afviser `null` for felter, der har en standardværdi.
- **Rettelse:** `SpeachesService` udelader nu `null`-felter i alle JSON-kald til Speaches (`JsonIgnoreCondition.WhenWritingNull`).
- **Filer:** `services/Speaches/SpeachesService.cs`
- **Verificeret:** ja. wav uden `speed`, mp3 med `speed`, flac og rå PCM ved 24 kHz giver alle 200, og et ugyldigt format giver stadig Speaches' 422.

### Fejl 3: `DownloadModel` svarede 500, selvom modellen blev hentet
- **Symptom:** `POST /Speaches/DownloadModel/{id}` gav 500 `'M' is an invalid start of a value`, men modellen var faktisk hentet.
- **Årsag:** Speaches svarer med ren tekst (`Model '…' downloaded`), og gatewayen prøvede at læse svaret som JSON.
- **Rettelse:** download og sletning læser ikke længere svarets indhold, som alligevel ikke bruges.
- **Filer:** `services/Speaches/SpeachesService.cs`, `services/Speaches/SpeachesService.Models.cs`
- **Verificeret:** ja. Download giver 204, gentaget download 204 og sletning 204.

### Fejl 4: Upload blev hængende i `Processing`, når embedding fejlede
- **Symptom:** fejlede Ollama under et upload (Ollama nede, embedding-modellen mangler eller timeout), fik klienten en fejl (502/404). Dokumentet og filen var dog allerede gemt og blev liggende med status `Processing` for altid.
- **Årsag:** kun tekstudtrækket var dækket af fejlhåndteringen. Embedding og indeksering lå uden for den.
- **Rettelse:** udtræk, embedding og indeksering har nu samme fejlhåndtering. Dokumentet får status `Failed` med årsagen i `ErrorMessage`, og svaret er 200, som allerede var den dokumenterede adfærd ved udtræksfejl. Klientens egen afbrydelse slipper stadig igennem. Fejlen logges som en advarsel.
- **Filer:** `managers/KnowledgeBase/KnowledgeBaseManager.cs`, `controllers/KnowledgeBaseController.cs` (kommentar), `managers/KnowledgeBase/Documents/KnowledgeBase.md`
- **Fundet:** ved kodegennemgang. **Verificeret:** ja (F1: Ollama nede giver 200 med `Status=Failed`).

### Fejl 5: Fejlsvar afslørede interne adresser
- **Symptom:** `detail` i alle videresendte fejl fra Ollama og Speaches indeholdt hele upstream-URL'en med intern IP-adresse og port. Det skete i alle miljøer, også Production. 502-svaret ved forbindelsesfejl indeholdt også IP og port, fordi .NET skriver dem i `HttpRequestException`.
- **Årsag:** beskederne i `OllamaException` og `SpeachesException` blev bygget med den fulde `RequestUri`, og `GlobalExceptionHandler` sendte `HttpRequestException.Message` videre til klienten.
- **Rettelse:** beskeden har nu kun stien (fx `Ollama request to '/api/show' failed with status 404 …`), og 502-svaret har en fast tekst (`An upstream service could not be reached.`). Samme beskyttelse gælder `ErrorMessage` på et dokument, der fejler. Exceptions logges stadig fuldt i `log.db`.
- **Filer:** `services/Ollama/OllamaService.cs`, `services/Speaches/SpeachesService.cs`, `infrastructure/ErrorHandling/GlobalExceptionHandler.cs`, `managers/KnowledgeBase/KnowledgeBaseManager.cs`, `services/*/Documents/*.md`
- **Verificeret:** ja. Alle fejltests kontrollerer, at `detail` ikke indeholder en adresse.

### Fejl 6: Realtime-transskription gav ingen forklaring, når Speaches ikke kunne nås
- **Symptom:** gatewayen opgraderede klientens forbindelse til WebSocket (101), før den forbandt til Speaches. Fejlede forbindelsen til Speaches, blev klientens socket bare afbrudt uden besked, fordi der ikke kan skrives ProblemDetails efter en upgrade.
- **Rettelse:** gatewayen forbinder nu til Speaches først og accepterer først derefter klienten (via en delegate, så servicen stadig ikke kender til HTTP). Fejler forbindelsen, får klienten et almindeligt 502 med ProblemDetails.
- **Filer:** `services/Speaches/SpeachesService.Realtime.cs`, `services/Speaches/Interfaces/ISpeachesService.cs`, `controllers/SpeachesController.cs`, `services/Speaches/Documents/Speaches.md`
- **Fundet:** ved kodegennemgang. **Verificeret:** ja (F1: klienten afvises før upgrade).

### Fejl 7: Realtime-transskription kvitterede ikke for klientens lukning
- **Symptom:** når klienten lukkede pænt, sendte gatewayen lukningen videre til Speaches, men Speaches' svar blev ikke sendt tilbage til klienten. Klienten fik derfor aldrig et close-frame og så en afbrudt forbindelse (1006) i stedet for 1000.
- **Rettelse:** et close-frame sendes nu også videre, når modtageren selv har startet lukningen (`CloseReceived`).
- **Filer:** `services/Speaches/SpeachesService.Realtime.cs`
- **Fundet:** ved kodegennemgang. **Verificeret:** nej. Det kræver en fungerende Speaches (se afsnit 5). Relayets fejlsti er dog verificeret: da Speaches afbrød forbindelsen, fik klienten et `error`-event med forklaringen og et pænt close med 1011.

### Fejl i selve testen (ikke i gatewayen)
Tre tests fejlede i kørsel 1 på grund af testscriptet, og de er rettet i scriptet:
- Python skrev databaseudtræk i cp1252, så æ, ø og å så forkerte ud. OCR-teksten i databasen var korrekt.
- Forventningen til whisper-small var for stram. Den hørte "gateway test" som "date-to-a-test".
- Efter fejl 2 sendte testen en fejl-body videre som "lyd" til de næste tests.

## 5. Blokeret af miljøet

Disse 10 tests kunne ikke verificeres. I alle tilfælde videresendte gatewayen Speaches' svar korrekt.

### Speaches gik i stå (7 tests)
- **Hvad:** efter en realtime-session i kørsel 1 hænger Speaches' tale-til-tekst og al model-load/unload (`api/ps`). Det gælder både `faster-whisper-small` og `-medium` og også ved direkte kald uden om gatewayen. Talesyntese, VAD og modellister virker stadig. `faster-whisper-small` står som indlæst og kan ikke stoppes.
- **Berørte tests:** `Transcribe` (2), `Translate`, realtime-transskription, `LoadModel`, `StopModel` (2).
- **Tidligere resultat:** i kørsel 1, før Speaches gik i stå, virkede `Transcribe` (mp3 gav 200 med engelsk tekst og segmenter) og `LoadModel` (200) gennem gatewayen.
- **Årsag:** kan ikke afgøres uden en genstart. Sessionen fik ugyldige lyddata (følgefejl af fejl 2) og blev lukket af klienten efter 90 sekunder. Det er mest sandsynligt en fejl i Speaches, for en unload bør aldrig hænge for evigt. Det kan dog ikke udelukkes, at den manglende lukke-kvittering (fejl 7) bidrog.
- **Handling:** genstart Speaches, og kør Speaches-testene igen. Opstår hænget igen efter en realtime-session gennem gatewayen, bør det samme forsøges direkte mod Speaches for at placere fejlen.

### Endpoints som Speaches v0.8.2 ikke har (2 tests)
`CreateSpeechEmbedding` (`/v1/audio/speech/embedding`) og `Diarize` (`/v1/audio/diarization`) findes ikke i den kørende Speaches. De står heller ikke i dens OpenAPI-spec, så gatewayen sender korrekt Speaches' 404 videre. Endpointene kræver en nyere Speaches, ellers bør de fjernes fra gatewayen.

### Speaches' chat completions (1 test)
`ChatCompletions` giver 500, også ved et minimalt kald direkte til Speaches. Speaches sender chat videre til en LLM-backend, og den er sandsynligvis ikke sat op eller ikke tilgængelig fra Speaches (indstillingen `chat_completion_base_url`). Det er et konfigurationsproblem i Speaches.

## 6. Observationer og anbefalinger (ikke rettet)

1. **Request-loggen viser forkert statuskode (kræver en beslutning).** Serilogs request-log skriver `responded 500` på niveau Error for kald, hvor klienten faktisk fik 404, 415, 422 eller 502. Det sker, fordi `UseSerilogRequestLogging()` står inden for `UseExceptionHandler()` og ser exception'en, før handleren sætter den rigtige status. Det gør `Logs/Search?Level=Error` misvisende. Rettelsen er at flytte `UseSerilogRequestLogging()` før `UseExceptionHandler()`, men det strider mod reglen i `.claude/project.md` om, at `UseExceptionHandler` altid står først. Derfor er det ikke ændret.
2. **RAG-chat tager ca. 50 sekunder.** Ollama-serveren skifter mellem `nomic-embed-text` (søgning) og `qwen2.5:14b-instruct` (svar) ved hvert RAG-kald. Første `Generate` tog 47 s, mens de efterfølgende chat-kald tog 0,2-0,7 s. Det er ikke gatewayen, der er langsom. Sæt `OLLAMA_MAX_LOADED_MODELS` til mindst 2 på Ollama-serveren, hvis grafikkortets hukommelse rækker.
3. **Docker er ikke testet, og imaget bliver stort.** Docker er ikke installeret på testmaskinen, så imaget er ikke bygget. En Release-publish viser, at Linux-bibliotekerne (`libSkiaSharp.so`, `libpdfium.so`, `vec0.so`, `libe_sqlite3.so`) og Tesseracts sprogmodeller kommer med, og at Windows-Tesseract (`Engine/`) bliver ude. `runtimes/` fylder dog 633 MB af 691 MB, fordi der er native filer til alle platforme med. Med `-r linux-x64` på `restore` og `publish` i Dockerfilen ville det meste forsvinde.
4. **Speaches' modellister mister felter.** `ModelDto` har kun `id`, `created`, `object`, `owned_by`, `language` og `task`. Speaches sender også fx `voices` og `sample_rate` for TTS-modeller i registry, så en klient kan ikke se, hvilke stemmer en model har, før den er hentet.
5. **En delvis indeksering kan efterlade vektorer.** `KnowledgeDocumentRepository.MarkIndexedAsync` indsætter vektorerne i `vectors.db` én ad gangen, før `SaveChangesAsync`. Fejler den halvvejs, bliver de allerede indsatte vektorer liggende, og senere sletninger fjerner dem ikke. Det er usandsynligt i praksis (de realistiske fejl sker før første vektor), men en transaktion om metoden ville lukke hullet.

## 7. Ikke testet

- Docker-build og -kørsel (Docker er ikke installeret, se 6.3).
- `Ollama/PushModel` ud over fejlstien (kræver en konto på ollama.com). Fejlen videresendes korrekt.
- `Ollama/PullModel` af en ny model. Kun en allerede hentet model er testet, for at undgå flere GB download.
- `LogCleanupWorker`'s oprydning ved 1 GiB.
- `https`-profilen (kun `http` er testet).
- Belastning og ydeevne ud over de målte svartider i bilaget.

## 8. Ændrede filer

Alle stier er under `api/`.

| Fil | Fejl |
|---|---|
| `controllers/KnowledgeBaseController.cs` | 4 (kommentar) |
| `controllers/SpeachesController.cs` | 6 |
| `dtos/Speaches/ListVoicesSpeachesResponseDto.cs`, `dtos/Speaches/VoiceDto.cs` (ny), `dtos/Speaches/SpeachesMapper.cs` | 1 |
| `infrastructure/ErrorHandling/GlobalExceptionHandler.cs` | 5 |
| `managers/KnowledgeBase/KnowledgeBaseManager.cs` | 4, 5 |
| `services/Ollama/OllamaService.cs` | 5 |
| `services/Speaches/SpeachesService.cs` | 2, 3, 5 |
| `services/Speaches/SpeachesService.Models.cs` | 1, 3 |
| `services/Speaches/SpeachesService.Realtime.cs` | 6, 7 |
| `services/Speaches/Interfaces/ISpeachesService.cs` | 1, 6 |
| `services/Speaches/Dtos/Models/ListVoicesResponseDto.cs`, `VoiceDto.cs` (nye) | 1 |
| `managers/KnowledgeBase/Documents/KnowledgeBase.md`, `services/Ollama/Documents/Ollama.md`, `services/Speaches/Documents/Speaches.md`, `.claude/CLAUDE.md` | Dokumentation |

## Bilag A: Alle tests

Resultaterne er fra kørsel 2 (efter rettelserne) og fejlscenarierne F1/F2. Tiderne er målt fra testklienten.

### Pr. område

| Område | Tests | Bestået | Fejlet | Blokeret af miljøet |
|---|---:|---:|---:|---:|
| Platform (Health, Swagger, OpenAPI) | 4 | 4 | 0 | 0 |
| Authentication | 4 | 4 | 0 | 0 |
| Keys | 20 | 20 | 0 | 0 |
| KnowledgeBase (inkl. Tesseract) | 45 | 45 | 0 | 0 |
| Ollama | 20 | 20 | 0 | 0 |
| Speaches | 30 | 20 | 0 | 10 |
| Logs | 8 | 8 | 0 | 0 |
| Sletning af nøgler og oprydning | 3 | 3 | 0 | 0 |
| Fejlscenarier (ekstra instanser) | 12 | 12 | 0 | 0 |
| **I alt** | **146** | **136** | **0** | **10** |

### Platform (Health, Swagger, OpenAPI)

| Endpoint | Test | Forventet | Resultat | Tid |
|---|---|---|---|---:|
| `GET /Health/Check` | anonym health check | 200 + status | ✅ Bestået | 207 ms |
| `GET /` | redirect til Swagger | 302 Location /swagger | ✅ Bestået | 4 ms |
| `GET /swagger/index.html` | Swagger UI loader | 200 html | ✅ Bestået | 58 ms |
| `GET /openapi/v1.json` | OpenAPI-dokument anonymt, titel, badge, security, ingen servers | 200 + krav opfyldt | ✅ Bestået | 415 ms |

### Authentication

| Endpoint | Test | Forventet | Resultat | Tid |
|---|---|---|---|---:|
| `GET /Ollama/GetVersion` | uden nøgle | 401 ProblemDetails | ✅ Bestået | 17 ms |
| `GET /Ollama/GetVersion` | ugyldig nøgle | 401 ProblemDetails | ✅ Bestået | 329 ms |
| `GET /Ollama/GetVersion` | master-nøgle | 200 | ✅ Bestået | 97 ms |
| `GET /KnowledgeBase/ListGroups` | master-nøgle på StandardKeyOnly | 403 ProblemDetails | ✅ Bestået | 5 ms |

### Keys

| Endpoint | Test | Forventet | Resultat | Tid |
|---|---|---|---|---:|
| `POST /Keys/Create` | opret standard-nøgle A | 201 + apiKey + Location | ✅ Bestået | 395 ms |
| `POST /Keys/Create` | opret standard-nøgle B (til isolation) | 201 | ✅ Bestået | 24 ms |
| `POST /Keys/Create` | validering: tomt navn | 400 ProblemDetails med errors | ✅ Bestået | 34 ms |
| `POST /Keys/Create` | validering: ExpiresAt i fortiden | 400 | ✅ Bestået | 5 ms |
| `POST /Keys/Create` | standard-nøgle må ikke administrere nøgler | 403 | ✅ Bestået | 43 ms |
| `GET /Keys/List` | liste uden rå nøgle/hash | 200 med A og B | ✅ Bestået | 33 ms |
| `GET /Keys/GetById/{id}` | hent A | 200 | ✅ Bestået | 30 ms |
| `GET /Keys/GetById/{id}` | ukendt id | 404 ProblemDetails | ✅ Bestået | 6 ms |
| `GET /Keys/GetById/{id}` | ugyldigt id-format | 404 (route-constraint) | ✅ Bestået | 3 ms |
| `PUT /Keys/Update/{id}` | opdatér A | 200 med nye værdier | ✅ Bestået | 99 ms |
| `PUT /Keys/Update/{id}` | ukendt id | 404 | ✅ Bestået | 4 ms |
| `PUT /Keys/Update/{id}` | validering: ExpiresAt i fortiden | 400 | ✅ Bestået | 3 ms |
| `standard-nøgle i brug` | A virker og LastUsedAt sættes | 200 + lastUsedAt | ✅ Bestået | 19 ms |
| `POST /Keys/Deactivate/{id}` | deaktivér A → nøglen afvises | 200 isActive=false, derefter 401 | ✅ Bestået | 30 ms |
| `POST /Keys/Activate/{id}` | aktivér A → nøglen virker igen | 200 isActive=true, derefter 200 | ✅ Bestået | 55 ms |
| `POST /Keys/Activate\|Deactivate/{id}` | ukendt id | 404 begge | ✅ Bestået | 9 ms |
| `POST /Keys/Rollover/{id}` | rollover A → gammel nøgle afvises, ny virker | 200, gammel 401, ny 200 | ✅ Bestået | 46 ms |
| `POST /Keys/Rollover/{id}` | ukendt id | 404 | ✅ Bestået | 7 ms |
| `GET /Keys/GetAuditLog/{id}` | audit-log for A | 200 med Created, Updated, Deactivated, Activated, RolledOver | ✅ Bestået | 74 ms |
| `GET /Keys/GetAuditLog/{id}` | ukendt id | 404 | ✅ Bestået | 5 ms |

### KnowledgeBase (inkl. Tesseract)

| Endpoint | Test | Forventet | Resultat | Tid |
|---|---|---|---|---:|
| `POST /KnowledgeBase/CreateGroup` | opret gruppe Personale | 200 | ✅ Bestået | 57 ms |
| `POST /KnowledgeBase/CreateGroup` | opret gruppe Drift | 200 | ✅ Bestået | 15 ms |
| `POST /KnowledgeBase/CreateGroup` | dublet-navn | 409 ProblemDetails | ✅ Bestået | 16 ms |
| `POST /KnowledgeBase/CreateGroup` | samme navn under en anden nøgle er tilladt | 200 | ✅ Bestået | 11 ms |
| `POST /KnowledgeBase/CreateGroup` | validering: tomt / for langt navn | 400 begge | ✅ Bestået | 14 ms |
| `GET /KnowledgeBase/ListGroups` | A ser kun egne grupper, sorteret | 200 [Drift, Personale] | ✅ Bestået | 29 ms |
| `POST /KnowledgeBase/UploadDocument` | upload ferie.txt | 200 Status=Indexed | ✅ Bestået | 374 ms |
| `POST /KnowledgeBase/UploadDocument` | upload it-politik.md | 200 Status=Indexed | ✅ Bestået | 78 ms |
| `POST /KnowledgeBase/UploadDocument` | upload rejsepolitik.docx | 200 Status=Indexed | ✅ Bestået | 277 ms |
| `POST /KnowledgeBase/UploadDocument` | upload tekst.pdf | 200 Status=Indexed | ✅ Bestået | 976 ms |
| `POST /KnowledgeBase/UploadDocument` | upload scannet.pdf | 200 Status=Indexed | ✅ Bestået | 4,5 s |
| `POST /KnowledgeBase/UploadDocument` | upload blandet.pdf | 200 Status=Indexed | ✅ Bestået | 1,7 s |
| `Tesseract (via upload)` | scannet PDF uden tekstlag OCR'es (dansk) | ExtractedText indeholder Kaffemaskinen, Bønnebryggeriet, Nøglen, Odin | ✅ Bestået | 246 ms |
| `Tesseract (via upload)` | blandet PDF: tekstlag + OCR af figur | tekstlag og 4711/havregryn/Vejle | ✅ Bestået | 328 ms |
| `UploadDocument (PDF tekstlag)` | tekst-PDF læses uden OCR, æøå bevaret | Gaffeltrucks, Brandøvelse | ✅ Bestået | 619 ms |
| `POST /KnowledgeBase/UploadDocument` | ikke-understøttet filtype (.exe) | 400 ProblemDetails | ✅ Bestået | 17 ms |
| `POST /KnowledgeBase/UploadDocument` | ukendt gruppe | 404 | ✅ Bestået | 13 ms |
| `POST /KnowledgeBase/UploadDocument` | anden nøgles gruppe | 404 | ✅ Bestået | 10 ms |
| `POST /KnowledgeBase/UploadDocument` | fil over MaxUploadSizeBytes (21 MB) | 413 ProblemDetails | ✅ Bestået | 631 ms |
| `POST /KnowledgeBase/UploadDocument` | tom fil gemmes som Failed | 200 Status=Failed + fejlbesked | ✅ Bestået | 71 ms |
| `POST /KnowledgeBase/UploadDocument` | mangler fil | 400 | ✅ Bestået | 7 ms |
| `GET /KnowledgeBase/ListDocuments` | alle dokumenter for A | 200, 7 dokumenter | ✅ Bestået | 40 ms |
| `GET /KnowledgeBase/ListDocuments` | filter på gruppe | 200, kun Drift (4) | ✅ Bestået | 19 ms |
| `GET /KnowledgeBase/ListDocuments` | B ser ingen af A's dokumenter | 200 [] | ✅ Bestået | 6 ms |
| `GET /KnowledgeBase/DownloadDocument/{id}` | download docx | 200, samme bytes, originalt filnavn | ✅ Bestået | 25 ms |
| `GET /KnowledgeBase/DownloadDocument/{id}` | download af Failed-dokument (tom fil) | 200, 0 bytes | ✅ Bestået | 8 ms |
| `GET /KnowledgeBase/DownloadDocument/{id}` | B kan ikke hente A's dokument / ukendt id | 404 begge | ✅ Bestået | 14 ms |
| `POST /KnowledgeBase/Search` | semantisk søgning: "Hvor mange ugers ferie har man?" | ferie.txt øverst | ✅ Bestået | 161 ms |
| `POST /KnowledgeBase/Search` | semantisk søgning: "Hvor lange skal adgangskoder være?" | it-politik.md øverst | ✅ Bestået | 38 ms |
| `POST /KnowledgeBase/Search` | semantisk søgning: "Hvor meget refunderes en hotelovernatning?" | rejsepolitik.docx øverst | ✅ Bestået | 39 ms |
| `POST /KnowledgeBase/Search` | semantisk søgning: "Hvem må køre gaffeltruck?" | tekst.pdf øverst | ✅ Bestået | 32 ms |
| `POST /KnowledgeBase/Search` | semantisk søgning: "Hvem servicerer kaffemaskinen?" | scannet.pdf øverst | ✅ Bestået | 41 ms |
| `POST /KnowledgeBase/Search` | semantisk søgning: "Hvor mange paller havregryn er der på lager?" | blandet.pdf øverst | ✅ Bestået | 40 ms |
| `POST /KnowledgeBase/Search` | TopK udeladt bruger DefaultTopK (5) | 200, 5 resultater | ✅ Bestået | 34 ms |
| `POST /KnowledgeBase/Search` | B finder intet (isolation) | 200 [] | ✅ Bestået | 29 ms |
| `POST /KnowledgeBase/Search` | validering: tom query / TopK=0 | 400 begge | ✅ Bestået | 11 ms |
| `POST /KnowledgeBase/RagChat` | RAG-svar med qwen2.5:14b-instruct | 200, svar nævner 5/fem, kilder med ferie.txt | ✅ Bestået | 47,8 s |
| `POST /KnowledgeBase/RagChat` | uden relevant viden (nøgle B) | 200, ingen kilder | ✅ Bestået | 52,8 s |
| `POST /KnowledgeBase/RagChat` | validering: ingen user-besked / ingen model | 400 begge | ✅ Bestået | 15 ms |
| `POST /KnowledgeBase/RagChat` | ukendt model sendes videre fra Ollama | 404 ProblemDetails | ✅ Bestået | 3,1 s |
| `DELETE /KnowledgeBase/DeleteDocument/{id}` | slet ferie.txt: række, chunks, vektorer og fil fjernes | 204, intet tilbage | ✅ Bestået | 861 ms |
| `DELETE /KnowledgeBase/DeleteDocument/{id}` | slet igen / B sletter A's dokument | 404 begge | ✅ Bestået | 28 ms |
| `DELETE /KnowledgeBase/DeleteGroup/{id}` | B kan ikke slette A's gruppe | 404 | ✅ Bestået | 18 ms |
| `DELETE /KnowledgeBase/DeleteGroup/{id}` | slet Drift med 4 dokumenter: filer og vektorer fjernes | 204, intet tilbage | ✅ Bestået | 822 ms |
| `DELETE /KnowledgeBase/DeleteGroup/{id}` | slet igen | 404 | ✅ Bestået | 28 ms |

### Ollama

| Endpoint | Test | Forventet | Resultat | Tid |
|---|---|---|---|---:|
| `GET /Ollama/GetVersion` | version | 200 | ✅ Bestået | 13 ms |
| `GET /Ollama/ListModels` | modeller | 200 med nomic-embed-text | ✅ Bestået | 40 ms |
| `GET /Ollama/ListRunningModels` | kørende modeller | 200 | ✅ Bestået | 27 ms |
| `POST /Ollama/ShowModel` | vis nomic-embed-text | 200 med details | ✅ Bestået | 240 ms |
| `POST /Ollama/ShowModel` | ukendt model | 404 ProblemDetails med Ollamas fejl, uden intern adresse | ✅ Bestået | 17 ms |
| `POST /Ollama/Generate` | ikke-streaming | 200 done=true med tekst | ✅ Bestået | 47,1 s |
| `POST /Ollama/Generate` | streaming (NDJSON) | 200 application/x-ndjson, flere linjer, sidste done=true | ✅ Bestået | 460 ms |
| `POST /Ollama/Chat` | ikke-streaming | 200 assistant-svar | ✅ Bestået | 247 ms |
| `POST /Ollama/Chat` | streaming (NDJSON) | 200 flere linjer, sidste done=true | ✅ Bestået | 218 ms |
| `POST /Ollama/Chat` | tool calling | 200 med toolCalls | ✅ Bestået | 774 ms |
| `POST /Ollama/Chat` | struktureret output (format=json schema) | 200 gyldig JSON | ✅ Bestået | 579 ms |
| `POST /Ollama/Chat` | ukendt model | 404 | ✅ Bestået | 10 ms |
| `POST /Ollama/Embed` | to input | 200, 2 vektorer á 768 | ✅ Bestået | 3,2 s |
| `POST /Ollama/CopyModel` | kopiér nomic-embed-text → gw-test-copy | 204, findes i ListModels | ✅ Bestået | 44 ms |
| `POST /Ollama/CopyModel` | ukendt kilde | 404 | ✅ Bestået | 11 ms |
| `POST /Ollama/CreateModel` | opret gw-test-create fra LLM med system-prompt | 200 status=success | ✅ Bestået | 254 ms |
| `POST /Ollama/PullModel` | pull nomic-embed-text (allerede hentet) | 200 status=success | ✅ Bestået | 667 ms |
| `POST /Ollama/PushModel` | push uden navnerum/konto (kun fejlstien kan testes) | Ollamas fejl sendes videre som ProblemDetails, eller 200 med error | ✅ Bestået | 1,3 s |
| `DELETE /Ollama/DeleteModel` | slet gw-test-copy og gw-test-create | 204 begge, væk fra ListModels | ✅ Bestået | 48 ms |
| `DELETE /Ollama/DeleteModel` | ukendt model | 404 | ✅ Bestået | 15 ms |

### Speaches

| Endpoint | Test | Forventet | Resultat | Tid |
|---|---|---|---|---:|
| `GET /Speaches/ListModels` | alle modeller | 200 med STT og TTS | ✅ Bestået | 92 ms |
| `GET /Speaches/ListModels` | filter Task=text-to-speech | 200, kun TTS | ✅ Bestået | 50 ms |
| `GET /Speaches/ListAudioModels` | lydmodeller | 200 models[] | ✅ Bestået | 57 ms |
| `GET /Speaches/ListVoices` | stemmer | 200 med stemmerne talesyntese og arctic (som Speaches selv returnerer) | ✅ Bestået | 59 ms |
| `GET /Speaches/GetModel/{modelId}` | model-id med skråstreg (%2F) | 200 | ✅ Bestået | 73 ms |
| `GET /Speaches/GetModel/{modelId}` | ukendt model | 404 ProblemDetails uden intern adresse | ✅ Bestået | 64 ms |
| `GET /Speaches/GetRegistry` | registry filtreret på STT | 200, kun STT | ✅ Bestået | 673 ms |
| `POST /Speaches/SynthesizeSpeech` | dansk tale som wav uden speed (null må ikke sendes) | 200 audio/wav, RIFF, Content-Disposition speech.wav | ✅ Bestået | 2,0 s |
| `POST /Speaches/SynthesizeSpeech` | engelsk tale som mp3 med speed (audio/mp3 → audio/mpeg) | 200 audio/mpeg | ✅ Bestået | 1,8 s |
| `POST /Speaches/SynthesizeSpeech` | flac | 200 audio/flac | ✅ Bestået | 124 ms |
| `POST /Speaches/SynthesizeSpeech` | rå PCM med sample rate 24000 | 200 PCM-bytes | ✅ Bestået | 155 ms |
| `POST /Speaches/SynthesizeSpeech` | ugyldigt format (opus) | 422 ProblemDetails med Speaches' valideringsfejl | ✅ Bestået | 12 ms |
| `POST /Speaches/SynthesizeSpeech` | ukendt model | 4xx ProblemDetails fra Speaches | ✅ Bestået | 9 ms |
| `POST /Speaches/Transcribe` | dansk wav med ord-tidsstempler | 200, tekst med 'ferie'/'kaffe', words[] | ⛔ Blokeret<br>Speaches tale-til-tekst hænger (bekræftet ved direkte kald uden om gatewayen). Kræver genstart af Speaches. | 303,8 s |
| `POST /Speaches/Transcribe` | mp3 uden sprog (auto-detektion) | 200, engelsk, segmenter | ⛔ Blokeret<br>Speaches tale-til-tekst hænger (bekræftet ved direkte kald uden om gatewayen). Kræver genstart af Speaches. | 304,1 s |
| `POST /Speaches/Transcribe` | ukendt model | 4xx ProblemDetails | ✅ Bestået | 178 ms |
| `POST /Speaches/Translate` | dansk tale → engelsk tekst | 200 med engelsk tekst | ⛔ Blokeret<br>Speaches tale-til-tekst hænger (bekræftet ved direkte kald uden om gatewayen). Kræver genstart af Speaches. | 304,2 s |
| `POST /Speaches/DetectSpeechTimestamps` | VAD på dansk wav | 200, mindst ét interval | ✅ Bestået | 273 ms |
| `POST /Speaches/CreateSpeechEmbedding` | speaker-embedding | 200 med embedding | ⛔ Blokeret<br>Speaches v0.8.2 har ikke endpointet. Gatewayen sender 404 korrekt videre. | 21 ms |
| `POST /Speaches/Diarize` | diarization | 200 med segmenter | ⛔ Blokeret<br>Speaches v0.8.2 har ikke endpointet. Gatewayen sender 404 korrekt videre. | 31 ms |
| `POST /Speaches/ChatCompletions` | OpenAI-kompatibel chat via Speaches (qwen2.5:14b-instruct) | 200 med choices | ⛔ Blokeret<br>Speaches fejler internt, også ved direkte kald uden om gatewayen (Speaches' chat-backend er ikke sat op). | 45 ms |
| `GET /Speaches/TranscribeRealtime` | almindeligt HTTP-kald (ikke WebSocket) | 400 ProblemDetails | ✅ Bestået | 13 ms |
| `WS /Speaches/TranscribeRealtime` | WebSocket uden nøgle | afvist (401 før upgrade) | ✅ Bestået | 6 ms |
| `POST /Speaches/DownloadModel + DELETE /Speaches/DeleteModel` | hent en lille piper-stemme og slet den igen | 204 / 204, modellen er væk igen | ✅ Bestået | 2,4 s |
| `DELETE /Speaches/DeleteModel/{modelId}` | model der ikke er installeret | 404 ProblemDetails | ✅ Bestået | 8 ms |
| `WS /Speaches/TranscribeRealtime` | WebSocket-relay: send PCM, modtag transskription, luk pænt | session.created + transskription + close 1000 | ⛔ Blokeret<br>Speaches tale-til-tekst hænger (bekræftet ved direkte kald uden om gatewayen). Kræver genstart af Speaches. | 645 ms |
| `POST /Speaches/LoadModel/{modelId}` | indlæs Systran/faster-whisper-medium | 200, vises i ListRunningModels | ⛔ Blokeret<br>Speaches' modelhåndtering hænger (også ved direkte kald). Kræver genstart af Speaches. | 60,1 s |
| `GET /Speaches/ListRunningModels` | kørende modeller | 200 models[] | ✅ Bestået | 8 ms |
| `DELETE /Speaches/StopModel/{modelId}` | stop Systran/faster-whisper-medium | 200, væk fra ListRunningModels | ⛔ Blokeret<br>Speaches' modelhåndtering hænger (også ved direkte kald). Kræver genstart af Speaches. | 60,0 s |
| `DELETE /Speaches/StopModel/{modelId}` | model der ikke kører | 4xx ProblemDetails | ⛔ Blokeret<br>Speaches' modelhåndtering hænger. Kræver genstart af Speaches. | 60,0 s |

### Logs

| Endpoint | Test | Forventet | Resultat | Tid |
|---|---|---|---|---:|
| `GET /Logs/Search` | standard-nøgle | 403 | ✅ Bestået | 20 ms |
| `GET /Logs/Search` | standard-søgning | 200, items og totalCount | ✅ Bestået | 47 ms |
| `GET /Logs/Search` | Level=Error finder de fremprovokerede fejl | 200, kun Error, mindst én | ✅ Bestået | 17 ms |
| `GET /Logs/Search` | Search i besked/exception | 200, alle matcher | ✅ Bestået | 13 ms |
| `GET /Logs/Search` | From/To (UTC) afgrænser korrekt | kun rækker i intervallet; fremtidigt From giver 0 | ✅ Bestået | 18 ms |
| `GET /Logs/Search` | paging | side 1 og 2 forskellige, pageSize=3 | ✅ Bestået | 10 ms |
| `GET /Logs/Search` | validering: PageSize=501 / Page=0 | 400 begge | ✅ Bestået | 6 ms |
| `Serilog → log.db` | request-log, ingen API-nøgler i log.db | ingen rå nøgler i log.db | ✅ Bestået | 399 ms |

### Sletning af nøgler og oprydning

| Endpoint | Test | Forventet | Resultat | Tid |
|---|---|---|---|---:|
| `DELETE /Keys/Delete/{id}` | slet B med upload: filer og vektorer fjernes | 204, intet tilbage | ✅ Bestået | 848 ms |
| `DELETE /Keys/Delete/{id}` | slet igen | 404 | ✅ Bestået | 5 ms |
| `DELETE /Keys/Delete/{id}` | slet A (rydder resten af testdata) | 204, databasen er tom igen | ✅ Bestået | 480 ms |

### Fejlscenarier (ekstra instanser)

| Endpoint | Test | Forventet | Resultat | Tid |
|---|---|---|---|---:|
| `GET /Ollama/GetVersion` | Ollama kan ikke nås | 502 ProblemDetails uden intern adresse | ✅ Bestået | 2,4 s |
| `POST /Ollama/Chat (stream)` | Ollama kan ikke nås, streaming | 502 ProblemDetails uden intern adresse (før streamen starter) | ✅ Bestået | 2,1 s |
| `GET /Speaches/ListModels` | Speaches kan ikke nås | 502 ProblemDetails uden intern adresse | ✅ Bestået | 2,1 s |
| `WS /Speaches/TranscribeRealtime` | Speaches kan ikke nås | klienten får en forklaring (502 før upgrade eller et error-event) | ✅ Bestået | 2,1 s |
| `POST /KnowledgeBase/UploadDocument` | embedding fejler (Ollama nede): dokumentet må ikke hænge i Processing | 200, Status=Failed med fejlbesked uden intern adresse, ikke Processing | ✅ Bestået | 2,9 s |
| `POST /KnowledgeBase/Search` | Ollama nede | 502 ProblemDetails uden intern adresse | ✅ Bestået | 2,0 s |
| `GET /openapi/v1.json` | Production: badge er grøn, Swagger tilgængelig | brightgreen + /swagger 200 | ✅ Bestået | 907 ms |
| `POST /KnowledgeBase/UploadDocument` | Tesseract mangler: scannet PDF | 200 Status=Failed med Tesseract-fejl | ✅ Bestået | 2,3 s |
| `POST /KnowledgeBase/UploadDocument` | Tesseract mangler: tekst-PDF klarer sig uden OCR | 200 Status=Indexed | ✅ Bestået | 421 ms |
| `POST /KnowledgeBase/UploadDocument` | Tesseract mangler: blandet PDF beholder tekstlaget | 200 Status=Indexed | ✅ Bestået | 304 ms |
| `POST /KnowledgeBase/UploadDocument` | ødelagt PDF og ødelagt docx | 200 Status=Failed begge | ✅ Bestået | 69 ms |
| `DELETE /Keys/Delete/{id}` | oprydning efter F2 | 204 og ingen filer tilbage | ✅ Bestået | 369 ms |
