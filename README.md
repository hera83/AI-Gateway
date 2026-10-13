# AI Gateway

AI Gateway er et .NET 10 Web API (controller-baseret), der fungerer som et samlet, nøglestyret indgangspunkt foran interne AI-tjenester: [Ollama](https://ollama.com/) (LLM-inferens) og [Speaches](https://speaches.ai/) (tale-til-tekst og tekst-til-tale). Det har desuden en indbygget knowledge base til Retrieval-Augmented Generation (RAG) med OCR via [Tesseract](https://github.com/tesseract-ocr/tesseract). Formålet er ét sikkert og ensartet indgangspunkt med API-nøgler, audit-log og søgbare logs, i stedet for at eksponere tjenesterne direkte.

## Funktioner

| Område | Adgang | Beskrivelse |
|---|---|---|
| **Health** (`/Health/Check`) | Ingen nøgle | Selvtjek af processen (hukommelsesforbrug): 200 eller 503. Beregnet til load balancers og uptime-overvågning. |
| **API-nøgler** (`/Keys/*`) | Master-nøglen | Opret, rediger, rotér ("rollover"), aktivér/deaktivér og slet nøgler, med audit-log pr. nøgle og valgfri udløbsdato. Sletning fjerner også alt, nøglen ejer i knowledge base. |
| **Ollama** (`/Ollama/*`) | Alle nøgler | Generate, chat (også streaming som NDJSON), embeddings, model-administration og version. |
| **Speaches** (`/Speaches/*`) | Alle nøgler | Chat completions, transskribering (også realtime over WebSocket), oversættelse, talesyntese, speaker-embeddings, VAD-tidsstempler, diarization og model-administration. |
| **Knowledge base / RAG** (`/KnowledgeBase/*`) | Standard-nøgler | Hver standard-nøgle har sin egen private vidensbank med grupper, dokumenter, søgning, RAG-chat og en research-agent. Se [Knowledge base](#knowledge-base). |
| **Logs** (`/Logs/Search`) | Master-nøglen | Søgning i applikationens logs (niveau, fritekst, tidsinterval, paging). |
| **Swagger UI** (`/swagger`) | Ingen nøgle (kald kræver nøgle) | Interaktiv dokumentation med **Authorize**-knap til `x-api-key` og et badge, der viser miljøet. Tilgængelig i alle miljøer. `GET /` sender videre hertil. |

## Knowledge base

Knowledge base kræver en **standard-nøgle**. Master-nøglen kan ikke eje data og får 403. Data er fuldstændig adskilt mellem nøgler: en anden nøgles grupper og dokumenter svarer 404.

| Endpoint | Gør |
|---|---|
| `CreateGroup`, `ListGroups`, `DeleteGroup/{id}` | Grupper. Sletning af en gruppe fjerner også dens dokumenter, vektorer og filer. |
| `UploadDocument` (multipart: `GroupId` + `File`) | Gemmer filen og svarer straks `202` med status `Processing`. En baggrundsworker udtrækker tekst, laver Markdown, chunker, embedder og indekserer, hvorefter status bliver `Indexed` (eller `Failed` med årsagen i `ErrorMessage`). |
| `ListDocuments`, `DownloadDocument/{id}`, `DeleteDocument/{id}` | Dokumenter (evt. filtreret på gruppe) og den originale fil. |
| `GetDocumentText/{id}` | Den udtrukne tekst og dokumentets status. `Location`-headeren fra upload peger hertil. |
| `ExportDocumentMarkdown/{id}` | Dokumentet som Markdown (`text/markdown`, `<navn>.md`) med overskrifter, lister og tabeller fra filens egen struktur. `409` før `Indexed`. Headeren `X-Markdown-Source` viser, hvordan den er lavet (`Structure`, `LlmPending` eller `Llm`), og `X-Markdown-Llm-Progress` viser fremskridtet (fx `12/50`), mens en LLM lægger skannede dokumenter op. |
| `Search` | Vektorsøgning i alle nøglens grupper eller kun én (`GroupId`). |
| `RagChat` | Én søgning på den seneste brugerbesked og ét svar fra en Ollama-model med kildehenvisninger. |
| `AgentChat` | Research-agent: spørger tilbage ved uklare opgaver (`NeedsClarification`), ellers søger den i flere runder og svarer i Markdown med `[n]`-henvisninger (`Answered`). `SkipClarification` springer spørgsmålene over. Tager fra få sekunder til et par minutter. |

**Filtyper:** alle rene tekstfiler (uanset endelse, fx `.txt`, `.md`, `.csv`, `.json`, kildekode), PDF, Word (`.docx`) og billeder (`.png`, `.jpg`/`.jpeg`/`.jfif`, `.tif`/`.tiff`, `.bmp`, `.gif`, `.webp`, `.jp2`, `.pnm`/`.pbm`/`.pgm`/`.ppm`). Andre binære filer (`.zip`, `.xlsx`, `.doc` …) afvises med 400. Højst 250 MiB pr. fil (`KnowledgeBase:MaxUploadSizeBytes`).

**OCR:** billeder, skannede PDF-sider og tekst i billeder i PDF'er læses med Tesseract (dansk + engelsk), og resultatet kvalitetstjekkes, før det indekseres. Består mindst halvdelen af et dokument af OCR-tekst, lægger `KnowledgeMarkdownWorker` dets Markdown op med en LLM via Ollama bagefter (`KnowledgeBase:Markdown:LlmModel`), og svaret kontrolleres ord for ord, så teksten aldrig ændres. Dokumentet er søgbart med det samme og indekseres igen, når LLM'en er færdig.

Detaljerne (chunking, OCR-tærskler, agentens trin, målinger) står i [api/managers/KnowledgeBase/Documents/KnowledgeBase.md](api/managers/KnowledgeBase/Documents/KnowledgeBase.md).

## Forudsætninger

- **Ollama** og **Speaches** kørende og tilgængelige fra gatewayen.
- Ollama-modeller til knowledge base:

  ```bash
  ollama pull nomic-embed-text        # embeddings (KnowledgeBase:EmbeddingModel), kræves til upload og søgning
  ollama pull qwen2.5:14b-instruct    # LLM-opsætning af skannede dokumenter (KnowledgeBase:Markdown:LlmModel), valgfri
  ```

  `RagChat` og `AgentChat` bruger den chatmodel, der angives i requestet (`Model`). Skiftes embedding-model eller -dimension, skal `vec_chunks` genskabes med en ny migration, og alle dokumenter embeddes igen.

## Kom i gang med Docker (anbefalet)

Kræver Docker Desktop (Windows/Mac) eller Docker Engine med Compose-plugin (Linux). Kør kommandoerne fra **løsningens rod** (mappen over `api/`).

1. Opret `.env` ud fra skabelonen, og udfyld `ApiKeys__MasterKey` (fx `openssl rand -hex 32`):

   ```bash
   cp .env.example .env
   ```

   | Variabel | Standard | Beskrivelse |
   |---|---|---|
   | `ApiKeys__MasterKey` | *(tom = slået fra)* | Master-nøglen til admin-endpoints. |
   | `Ollama__BaseUrl` | `http://host.docker.internal:11434` | Adressen på Ollama. `host.docker.internal` er Docker-værten selv (også på Linux via `extra_hosts`). |
   | `Speaches__BaseUrl` | `http://host.docker.internal:8000` | Adressen på Speaches. |

   Alle andre indstillinger fra `appsettings.json` kan også sættes i `.env` med `__` som separator, fx `KnowledgeBase__Markdown__LlmModel=`.

2. Byg og start:

   ```bash
   docker compose up --build -d
   ```

3. Tjek, at det virker: Swagger UI på [http://localhost:5000/swagger](http://localhost:5000/swagger) og `GET http://localhost:5000/Health/Check`.

Databaser og filer ligger i de navngivne volumes `api_app_dbs` og `api_app_files`, så de overlever `docker compose down`/`up` og genbygning. Kun `docker compose down -v` sletter dem. Imaget har Tesseract med. Containeren kører i dansk tid (`TZ=Europe/Copenhagen`), men alle tidspunkter gemmes i UTC. Containeren kører kun HTTP på port 5000; TLS hører til en reverse proxy foran.

```bash
docker compose logs -f          # følg logs
docker compose down             # stop (data bevares)
docker compose up --build -d    # genbyg og genstart efter kodeændringer
```

## Første skridt

1. Åbn Swagger UI, klik **Authorize**, og indsæt master-nøglen.
2. Opret en standard-nøgle med `POST /Keys/Create` (`name`, `responsibleName`, `contactInfo`, evt. `expiresAt`). Den rå nøgle (`ak_…`) vises **kun én gang** i svaret. Kun hash og preview gemmes.
3. Skift til standard-nøglen under **Authorize** for at bruge knowledge base: `CreateGroup` → `UploadDocument` → vent på status `Indexed` (`GetDocumentText`) → `Search`, `RagChat` eller `AgentChat`.

Med curl: `curl -H "x-api-key: <nøgle>" http://localhost:5000/Ollama/GetVersion`. Flere eksempler står i [api/api.http](api/api.http).

## Lokal udvikling uden Docker

Kræver [.NET 10 SDK](https://dotnet.microsoft.com/download). Fra løsningens rod:

```bash
dotnet run --project api --launch-profile http     # http://localhost:5207/swagger
dotnet run --project api --launch-profile https    # https://localhost:7161/swagger
```

I VS Code starter launch-konfigurationen `api` (F5) samme opsætning med debugger (`.vscode/` er lokal og ikke i git).

Lokalt bruges `appsettings.Development.json` (gitignored) til master-nøglen, adresserne på Ollama og Speaches og stien til Tesseract. Databaser oprettes og migreres automatisk ved opstart.

**Tesseract på Windows:** sprogmodellerne ligger i `api/services/Tesseract/Tessdata/` (i git). Selve programmet (~160 MB) er gitignored og skal ligge i `api/services/Tesseract/Engine/win-x64/`. Efter et frisk clone: installér [Tesseract 5](https://github.com/UB-Mannheim/tesseract/wiki), kopiér `tesseract.exe` og alle `.dll`-filer fra installationsmappen dertil, og afinstallér den igen. Se [Tesseract.md](api/services/Tesseract/Documents/Tesseract.md). Uden Tesseract virker tekstfiler, Word og PDF'er med tekstlag stadig, men billeder og skannede sider kan ikke læses.

## Konfiguration

Alle indstillinger står med standardværdier i [api/appsettings.json](api/appsettings.json). De vigtigste:

| Sektion | Indhold |
|---|---|
| `ApiKeys:MasterKey` | Master-nøglen. Secret, så den står aldrig i `appsettings.json`. Tom = slået fra. |
| `Ollama:*`, `Speaches:*` | `BaseUrl` og `TimeoutMinutes` (15 / 75). |
| `KnowledgeBase:*` | Embedding-model og -dimension, chunk-størrelse og -overlap, upload-loft, standard `TopK` og workerinterval. |
| `KnowledgeBase:Ocr:*` | OCR til/fra, sprog (`dan+eng`), DPI, kvalitetstærskler og parallelitet. |
| `KnowledgeBase:Markdown:*` | LLM-model til opsætning af skannede dokumenter (tom = ingen LLM) og dens tærskler. |
| `KnowledgeBase:Agent:*` | Research-agentens søgerunder, søgninger pr. runde og antal kilder. |
| `Tesseract:*` | Sti til programmet og sprogmodellerne samt timeout. |
| `Kestrel:Limits:MaxRequestBodySize` | Loft over request-størrelse (300 MB), også for multipart-uploads. |

## Arkitektur i korte træk

- **Autentificering:** headeren `x-api-key`. Master-nøglen giver adgang til admin-endpoints (`Keys`, `Logs`). Standard-nøgler gemmes kun som SHA-256-hash + preview og kan have en udløbsdato.
- **Fejl:** alle fejlsvar er RFC 9110 ProblemDetails (`application/problem+json`). Interne detaljer (fx Ollamas og Speaches' adresser) lækker ikke i Production.
- **Data** i `app_dbs/`: `app.db` (nøgler, audit-log og knowledge base-metadata, EF Core), `vectors.db` (embeddings via sqlite-vec, attached på hver `app.db`-forbindelse) og `log.db` (Serilog, holdes under 1 GiB).
- **Filer:** originale uploads ligger i `app_files/KnowledgeDocuments/` som `<guid>.<endelse>`.
- **Baggrundsjob** (`bgWorkers/`): behandling af uploads, LLM-opsætning af Markdown, oprydning af forældreløse filer og oprydning af `log.db`.
- **Struktur:** `controllers/` og `bgWorkers/` → `managers/` → `repositories/` (egen database og egne filer) og `services/` (Ollama, Speaches, Tesseract). Se `.claude/` i løsningens rod for de fulde konventioner.

## Yderligere dokumentation

- `.claude/` i løsningens rod og `api/.claude/CLAUDE.md`: konventioner og projektets opbygning (lokale filer, ikke i git).
- `api/repositories/*/Documents/`, `api/managers/*/Documents/` og `api/services/*/Documents/`: én beskrivelse pr. område.
- [api/api.http](api/api.http): eksempel-requests til de vigtigste endpoints.
- [rapport.md](rapport.md): testrapport fra end-to-end-testen af alle endpoints.

## Licens

[CC BY-NC 4.0](LICENSE.txt) (Creative Commons Attribution-NonCommercial 4.0 International).
