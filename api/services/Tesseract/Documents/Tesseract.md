# Tesseract (service)

**Formål:** Kører Tesseract OCR på ét billede og returnerer hvert genkendt ord med position og confidence (0-100).
**Brug det når:** tekst skal læses ud af et billede, fx en skannet PDF-side eller et diagram.
**Brug det ikke til:** at vurdere, om teksten er god nok. Servicen returnerer Tesseracts rå resultat, og kvalitetstjekket ligger hos kalderen (`OcrTextBuilder` i `managers/KnowledgeBase/`). Servicen rører aldrig `app.db`.
**Eksternt system og konfiguration:** `tesseract`-programmet (CLI, version 5), startet som en child process for hvert kald. Billedet sendes ind på stdin, og TSV kommer tilbage på stdout, så der bruges ingen midlertidige filer. Sektion `Tesseract` i `appsettings.json`: `ExecutablePath` (standard `tesseract` fra PATH), `TessdataPath` (standard `services/Tesseract/Tessdata`) og `TimeoutSeconds` (standard 120). Relative stier regnes ud fra projektmappen (`ContentRootPath`), og et rent programnavn findes på PATH. Light pattern uden `HttpClient`: konfigurationen læses ved hvert kald.

## Metoder
| Metode | Kommando | Beskrivelse |
|---|---|---|
| `RecognizeAsync(RecognizeRequestDto request)` | `tesseract stdin stdout -l <sprog> --psm <n> --dpi <n> -c tessedit_create_tsv=1` | OCR af ét billede (PNG, JPEG, TIFF …). Returnerer ordene (TSV level 5) i læserækkefølge med blok-, afsnits- og linjenummer, position og confidence. |

## Filer
```text
services/Tesseract/
├── Tessdata/            ← sprogmodellerne, i git og med i publish (også Docker)
│   ├── dan.traineddata  ← tessdata_best, tag 4.1.0
│   ├── eng.traineddata  ← tessdata_best, tag 4.1.0
│   └── osd.traineddata  ← orienteringsgenkendelse (--psm 1)
└── Engine/win-x64/      ← Tesseract til Windows (tesseract.exe + DLL'er, ~160 MB). Gitignored og aldrig i publish.
```

- **Windows:** `appsettings.Development.json` sætter `Tesseract:ExecutablePath` til `services/Tesseract/Engine/win-x64/tesseract.exe`. Mangler mappen (fx efter et frisk clone), så installér Tesseract 5 med UB Mannheims installer fra https://github.com/UB-Mannheim/tesseract/wiki, kopiér `tesseract.exe` og alle `.dll`-filer fra installationsmappen til `Engine/win-x64/`, og afinstallér den igen.
- **Docker:** `api/Dockerfile` installerer kun programmet (`tesseract-ocr` fra Ubuntu). Modellerne kommer fra `Tessdata/` i publish-output'et, så Windows og Docker bruger de samme filer.
- **Nye sprog:** læg `<kode>.traineddata` fra https://github.com/tesseract-ocr/tessdata_best i `Tessdata/`, og tilføj koden til `KnowledgeBase:Ocr:Languages`.

## Faldgruber
- Brug `tessdata_best`. Med standardmodellerne bliver æ, ø og å ofte til "ae", "o" og "a", så snart `eng` er indlæst sammen med `dan`.
- TSV slås til med `-c tessedit_create_tsv=1` og ikke med config-filen `tsv`. Den fil mangler i en selvbygget tessdata-mappe, og så giver Tesseract stille almindelig tekst i stedet.
- `--psm 1` (med orienteringsgenkendelse) kræver `osd.traineddata` i samme mappe som sprogmodellerne.
- `OMP_THREAD_LIMIT=1` sættes på processen, fordi kalderen selv kører flere sider parallelt.
- Kan programmet ikke startes, eller ender det med en exit-kode forskellig fra 0, kastes `InvalidOperationException` med Tesseracts stderr. En timeout giver `TimeoutException`, og processen bliver dræbt.
