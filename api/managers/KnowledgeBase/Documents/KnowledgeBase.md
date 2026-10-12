# KnowledgeBase (manager)

**Formål:** Giver hver standard-nøgle sin egen private vidensbank til RAG: upload med tekstudtræk (inkl. OCR af PDF'er), chunking og embedding, sletning af grupper med indhold, vektorsøgning og RAG-chat.
**Brug det når:** en handling kombinerer knowledge base-repositories med Ollama eller Tesseract, eller sletter en gruppe med dokumenter.
**Brug det ikke til:** simple opslag (`ListGroups`, `ListDocuments`, `DownloadDocument`, `CreateGroup`, `DeleteDocument`). De kalder repositoriet direkte (hybrid-reglen).
**Afhænger af:** `IKnowledgeGroupRepository`, `IKnowledgeDocumentRepository`, `IOllamaService`, `ITesseractService` og `KnowledgeBaseSettings` (sektion `KnowledgeBase` i `appsettings.json`, OCR under `KnowledgeBase:Ocr`).

## Metoder
| Metode | Beskrivelse |
|---|---|
| `UploadDocumentAsync(UploadDocumentDto dto)` | Tjekker gruppe, størrelse og filtype (`.txt`, `.md`, `.pdf`, `.docx`), gemmer filen, udtrækker tekst (PDF'er med OCR, se nedenfor), chunker, embedder med `KnowledgeBase:EmbeddingModel` og indekserer. Udfaldet er `UploadDocumentOutcome`. Fejler udtræk, embedding (fx Ollama nede eller modellen mangler) eller indeksering, er dokumentet stadig gemt med status `Failed` og årsagen i `ErrorMessage`. Det bliver aldrig hængende i `Processing`. |
| `DeleteGroupAsync(Guid apiKeyId, Guid knowledgeGroupId)` | Sletter gruppens dokumenter (rækker, vektorer og filer) og derefter gruppen. `false` hvis gruppen ikke findes. |
| `SearchAsync(Guid apiKeyId, string query, int? topK)` | Embedder forespørgslen og finder de mest relevante chunks på tværs af alle nøglens grupper. `topK` er som standard `KnowledgeBase:DefaultTopK`. |
| `RagChatAsync(RagChatDto dto)` | Søger på den seneste `user`-besked, bygger en ny system-prompt med de fundne chunks (erstatter en evt. medsendt system-besked) og chatter via Ollama. Returnerer svaret plus `Sources`. |

## Tekstudtræk fra PDF (`KnowledgeBaseManager.Extraction.cs`)
Hver side vurderes for sig:

| Sidens eget tekstlag | Det sker |
|---|---|
| Rigtig tekst | Tekstlaget bruges. Billeder på siden, der er store nok til at rumme tekst (mindst 72 × 18 punkter), OCR'es, og teksten tilføjes, hvis den består kvalitetstjekket og ikke allerede står på siden. Et billede, der fylder mindst 80 % af en side med meget tekst, springes over (baggrund, eller en skanning som skanneren allerede har OCR'et). Det samme billede på flere sider (logo, header) OCR'es kun én gang. |
| Mangler (under `MinCharacters` bogstaver og tal) | Skannet side: hele siden OCR'es. Afvises resultatet, beholdes det lille tekstlag (fx et sidetal). |
| Volapyk (fonte uden brugbar Unicode-mapping) | Hele siden OCR'es. Afvises resultatet, bliver siden tom. Volapykken kommer aldrig med. |

Sider renderes med PDFium (`PdfPageRenderer`) i gråtoner ved `RenderDpi`, én side ad gangen. Sider OCR'es parallelt (`MaxParallelism`). Hele sider køres med `--psm 3`. Afvises resultatet, og fandt Tesseract ord, prøves der igen med `--psm 1` (orienteringsgenkendelse), som læser sider på hovedet.

**Kvalitetstjek (`OcrTextBuilder` og `TextQuality`):**
1. Ord under `MinWordConfidence` fjernes (pletter og kanter læst som "|" eller "6,").
2. Linjer under `MinLineConfidence` og linjer uden et ord på mindst to bogstaver/tal fjernes.
3. Resultatet afvises, hvis under `MinKeptPercent` % af den genkendte tekst er tilbage (typisk sider på hovedet), hvis den gennemsnitlige confidence er under `MinConfidence`, hvis der er under `MinCharacters` bogstaver og tal, eller hvis teksten ikke ligner ord (for mange symboler, kontroltegn eller "ord" uden vokaler).

Uden OCR (`KnowledgeBase:Ocr:Enabled = false`) bruges kun tekstlaget, som før.

## Faldgruber
- Master-nøglen har ingen `ApiKeys`-række og kan ikke eje data. Controlleren afviser den med 403, før manageren kaldes.
- Upload, OCR og indeksering sker synkront i requestet. Der er ingen jobkø. En skannet side tager typisk 2-4 sekunder.
- Kan Tesseract ikke køre, logges en advarsel, og tekstlaget bruges alene. Har PDF'en intet tekstlag, får dokumentet status `Failed` med Tesseracts fejl i `ErrorMessage`.
- Positioner fra PdfPig (0.1.16) er allerede drejet efter sidens `/Rotate` og flyttet til MediaBox'ens origo, mens `CropBox` står i rå PDF-koordinater. `DocumentTextExtractor.ToPdfPageDto` regner begge om til den viste side. Tjek det, hvis PdfPig opgraderes.
- OCR-tærsklerne er kalibreret til Tesseract med `tessdata_best`. Hæves de, kommer der mindre støj med, men der mistes også flere rigtige ord.
- `DocumentTextExtractor`, `PdfPageRenderer`, `OcrTextBuilder` og `TextQuality` ligger her og ikke i `extensions/`, fordi kun knowledge base bruger dem.
- Skiftes embedding-model eller -dimension, skal `vec_chunks` genskabes, og alle dokumenter skal embeddes igen.
