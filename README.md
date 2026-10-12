# AI Gateway

AI Gateway er et .NET 10 Web API (controller-baseret), der fungerer som et samlet, nøglestyret indgangspunkt foran interne AI-tjenester: [Ollama](https://ollama.com/) (LLM-inferens) og [Speaches](https://speaches.ai/) (tale-til-tekst og tekst-til-tale). Det har desuden en indbygget knowledge base til Retrieval-Augmented Generation (RAG). Formålet er ét sikkert og ensartet indgangspunkt med API-nøgler, audit-log og søgbare logs, i stedet for at eksponere tjenesterne direkte.

## Funktioner

| Område | Beskrivelse |
|---|---|
| **Health** (`/Health/Check`) | Selvtjek af processen. Kræver ingen nøgle og er beregnet til load balancers og uptime-overvågning. |
| **API-nøgler** (`/Keys/*`) | Opret, rediger, rotér ("rollover"), aktivér/deaktivér og slet nøgler, med audit-log pr. nøgle. Kun master-nøglen. |
| **Ollama** (`/Ollama/*`) | Generate, chat (også streaming), embeddings, model-administration og version. |
| **Speaches** (`/Speaches/*`) | Chat completions, transskribering (også realtime over WebSocket), oversættelse, talesyntese, speaker-embeddings, VAD-tidsstempler, diarization og model-administration. |
| **Knowledge base / RAG** (`/KnowledgeBase/*`) | Hver standard-nøgle har sin egen private vidensbank: grupper, upload af txt/md/pdf/docx (udtrækkes, chunkes og embeddes), søgning og RAG-chat. Skannede PDF-sider og tekst i billeder læses med Tesseract OCR, og resultatet kvalitetstjekkes, før det indekseres. Data er fuldstændig adskilt mellem nøgler. |
| **Logs** (`/Logs/Search`) | Søgning i applikationens logs (niveau, fritekst, tidsinterval, paging). Kun master-nøglen. |
| **Swagger UI** (`/swagger`) | Interaktiv dokumentation med **Authorize**-knap til `x-api-key` og et badge, der viser miljøet. Tilgængelig i alle miljøer. |

## Arkitektur i korte træk

- **Autentificering:** headeren `x-api-key`. Master-nøglen står i `ApiKeys:MasterKey` og giver adgang til admin-endpoints. Standard-nøgler oprettes via `/Keys/Create` og gemmes kun som SHA-256-hash + preview.
- **Fejl:** alle fejlsvar er RFC 9110 ProblemDetails (`application/problem+json`).
- **Data** i `app_dbs/`: `app.db` (nøgler og knowledge base-metadata, EF Core), `vectors.db` (embeddings via sqlite-vec) og `log.db` (Serilog).
- **Filer:** originale uploads ligger i `app_files/KnowledgeDocuments/` som `<guid>.<endelse>`.
- **Struktur:** `controllers/` → `managers/` → `repositories/` (egen database og egne filer) og `services/` (Ollama, Speaches, Tesseract). Se `.claude/` i løsningens rod for de fulde konventioner.

## Kom i gang med Docker (anbefalet)

Kræver Docker Desktop (Windows/Mac) eller Docker Engine med Compose-plugin (Linux). Kør kommandoerne fra **løsningens rod** (mappen over `api/`).

1. Opret `.env` ud fra skabelonen, og udfyld `ApiKeys__MasterKey` (fx `openssl rand -hex 32`):

   ```bash
   cp .env.example .env
   ```

   | Variabel | Standard | Beskrivelse |
   |---|---|---|
   | `ApiKeys__MasterKey` | *(tom = slået fra)* | Master-nøglen til admin-endpoints. |
   | `Ollama__BaseUrl` | `http://host.docker.internal:11434` | Adressen på Ollama. `host.docker.internal` er Docker-værten selv. |
   | `Speaches__BaseUrl` | `http://host.docker.internal:8000` | Adressen på Speaches. |

2. Byg og start:

   ```bash
   docker compose up --build -d
   ```

3. Tjek at det virker: Swagger UI på [http://localhost:5000/swagger](http://localhost:5000/swagger) og `GET http://localhost:5000/Health/Check`. Klik **Authorize** i Swagger, og indsæt master-nøglen for at prøve de beskyttede endpoints.

Databaser og filer ligger i de navngivne volumes `api_app_dbs` og `api_app_files`, så de overlever `docker compose down`/`up` og genbygning. Kun `docker compose down -v` sletter dem. Containeren kører i dansk tid (`TZ=Europe/Copenhagen`), men alle tidspunkter gemmes i UTC.

```bash
docker compose logs -f          # følg logs
docker compose down             # stop (data bevares)
docker compose up --build -d    # genbyg og genstart efter kodeændringer
```

## Lokal udvikling uden Docker

Kræver [.NET 10 SDK](https://dotnet.microsoft.com/download). Fra løsningens rod:

```bash
dotnet run --project api --launch-profile http    # http://localhost:5207/swagger
```

Lokalt bruges `appsettings.Development.json` (gitignored) til master-nøglen og adresserne på Ollama og Speaches. Migrations køres automatisk ved opstart.

OCR af PDF'er bruger Tesseract. Sprogmodellerne ligger i `api/services/Tesseract/Tessdata/` (i git). Selve Windows-programmet (~160 MB) er gitignored og skal ligge i `api/services/Tesseract/Engine/win-x64/`. Efter et frisk clone: installér [Tesseract 5](https://github.com/UB-Mannheim/tesseract/wiki), kopiér `tesseract.exe` og alle `.dll`-filer fra installationsmappen dertil, og afinstallér den igen. Se `api/services/Tesseract/Documents/Tesseract.md`. Uden Tesseract virker PDF'er med tekstlag stadig, men skannede sider kan ikke læses. Docker-imaget har Tesseract med.

## Yderligere dokumentation

- `.claude/` i løsningens rod og `api/.claude/CLAUDE.md`: konventioner og projektets opbygning.
- `repositories/*/Documents/`, `managers/*/Documents/` og `services/*/Documents/`: én beskrivelse pr. område.
- `api.http`: eksempel-requests til de vigtigste endpoints.
