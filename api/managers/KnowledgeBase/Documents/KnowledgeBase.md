# KnowledgeBase (manager)

**Formål:** Giver hver standard-nøgle sin egen private vidensbank til RAG: upload med tekstudtræk (inkl. OCR af PDF'er og billeder), chunking og embedding, sletning af grupper med indhold, vektorsøgning og RAG-chat.
**Brug det når:** en handling kombinerer knowledge base-repositories med Ollama eller Tesseract, eller sletter en gruppe med dokumenter.
**Brug det ikke til:** simple opslag (`ListGroups`, `ListDocuments`, `DownloadDocument`, `CreateGroup`, `DeleteDocument`). De kalder repositoriet direkte (hybrid-reglen).
**Afhænger af:** `IKnowledgeGroupRepository`, `IKnowledgeDocumentRepository`, `IOllamaService`, `ITesseractService`, `KnowledgeDocumentProcessingSignal` (singleton) og `KnowledgeBaseSettings` (sektion `KnowledgeBase` i `appsettings.json`, OCR under `KnowledgeBase:Ocr`).

## Metoder
| Metode | Beskrivelse |
|---|---|
| `UploadDocumentAsync(UploadDocumentDto dto)` | Tjekker gruppe, størrelse og filtype (se "Filtyper" nedenfor), gemmer filen med status `Processing` og vækker `KnowledgeDocumentProcessingWorker` via `KnowledgeDocumentProcessingSignal`. Udfaldet er `UploadDocumentOutcome`. Selve behandlingen sker i `ProcessNextDocumentAsync`. |
| `ProcessNextDocumentAsync()` | Kaldes kun af `KnowledgeDocumentProcessingWorker`. Tager det ældste dokument i `Processing` (alle nøgler), udtrækker tekst og Markdown i samme gennemløb (PDF'er og billeder med OCR, se nedenfor), chunker, embedder med `KnowledgeBase:EmbeddingModel` og indekserer. Venter intet i `Processing`, får det ældste `Indexed` dokument uden Markdown, eller med Markdown fra en ældre `MarkdownFormatter.Version`, sin Markdown lavet igen og bliver indekseret igen fra den. Derefter indekseres dokumenter med chunks fra en ældre `MarkdownChunker.Version` igen fra deres Markdown (kun embeddings, ingen ny OCR). Fejler udtræk, embedding (fx Ollama nede eller modellen mangler) eller indeksering, får dokumentet status `Failed` med årsagen i `ErrorMessage`. `false` hvis intet ventede. |
| `ImproveNextMarkdownAsync()` | Kaldes kun af `KnowledgeMarkdownWorker`. Lægger Markdown for det ældste dokument i `LlmPending` op med LLM'en (se "LLM-opsætning af OCR-tekst"). `false` hvis intet ventede. |
| `DeleteGroupAsync(Guid apiKeyId, Guid knowledgeGroupId)` | Sletter gruppens dokumenter (rækker, vektorer og filer) og derefter gruppen. `false` hvis gruppen ikke findes. |
| `SearchAsync(Guid apiKeyId, string query, int? topK, Guid? knowledgeGroupId)` | Embedder forespørgslen og finder de mest relevante chunks på tværs af alle nøglens grupper, eller kun i `knowledgeGroupId`. `null` hvis gruppen ikke findes (eller tilhører en anden nøgle). `topK` er som standard `KnowledgeBase:DefaultTopK`. |
| `AgentChatAsync(AgentChatDto dto)` | Research-agent over nøglens dokumenter (evt. én gruppe), se "AgentChat" nedenfor. Udfaldet er `AgentChatOutcome`: `NeedsClarification` (spørgsmål tilbage) eller `Answered` (svar i Markdown med `[n]`-henvisninger til `Sources`). `null` hvis gruppen ikke findes. |
| `RagChatAsync(RagChatDto dto)` | Søger (via `SearchAsync`) på den seneste `user`-besked i alle nøglens grupper eller kun i `dto.KnowledgeGroupId`. `null` hvis gruppen ikke findes. Bygger derefter en ny system-prompt med de fundne chunks (erstatter en evt. medsendt system-besked) og chatter via Ollama. Returnerer svaret plus `Sources`. |

## Filtyper (`DocumentTextExtractor.DetectKindAsync`)
| Fil | Genkendes på | Læses sådan |
|---|---|---|
| PDF | `.pdf` | Tekstlag + OCR, se nedenfor. |
| Word | `.docx` | Afsnittenes tekst (OpenXml). |
| Billeder | `.png`, `.jpg`, `.jpeg`, `.jfif`, `.tif`, `.tiff`, `.bmp`, `.gif`, `.webp`, `.jp2`, `.pnm`, `.pbm`, `.pgm`, `.ppm` | OCR af hele billedet, se nedenfor. |
| Alt andet | Indholdet: de første 8 KB må ikke indeholde NUL-bytes (undtagen UTF-16/32 med BOM) og højst 5 % kontroltegn | Som tekst. En BOM afgør UTF-8/16/32. Uden BOM prøves UTF-8, og ugyldig UTF-8 læses som Windows-1252. Dermed virker alle rene tekstformater (`.txt`, `.md`, `.csv`, `.json`, `.xml`, `.html`, `.log`, kildekode, filer uden endelse …). HTML og RTF indekseres som de står, med tags/kontrolord. |

Binære filer, der ikke er på listen (`.zip`, `.exe`, `.xlsx`, `.pptx`, `.doc` …), afvises ved upload med `UnsupportedFileType`.

## Indeksering (`MarkdownChunker`)
Chunks laves af dokumentets Markdown, ikke af den rå tekst, så sidehoveder/-fødder fra skanninger ikke er med i hver chunk og gør alle sider ens for vektorsøgningen.

- En chunk er hele afsnit, lister og tabeller op til `ChunkSizeCharacters`. `#` og `##` starter altid en ny chunk (dokumentets dele eller indførsler); dybere overskrifter og fede etiketter (`**Plan**`) kun, når chunken er mindst halvt fuld, så korte afsnit bliver hos deres naboer.
- Hver chunk starter med overskrifterne og etiketten over den (fx `## Notat 27.08` / `### Plan`), så både søgningen og chatmodellen ved, hvor teksten hører til.
- Et afsnit, der er længere end en chunk, deles ved linjer, så sætninger, så tegn, med `ChunkOverlapCharacters` overlap.
- Indekset følger altid den aktuelle Markdown: et upload indekseres fra strukturens Markdown, og når LLM'en er færdig med sin version, eller Markdown laves igen, indekseres dokumentet igen (`ReplaceChunksAsync`). `KnowledgeDocuments.IndexVersion` er `MarkdownChunker.Version` (0 = chunks af den rå tekst); hæv den, når opdelingen ændres.
- Er Markdown tom, chunkes den rå tekst med faste vinduer som før.

**Målt på en skannet journal på 60 sider:** 184 chunks, 183 med overskrift/etiket foran, udskriftsstemplet i 1. Konkrete spørgsmål ("BMI ved kontrollen", "hvem kan man ringe til") finder den rigtige chunk på plads 1-3. Et hvorfor-spørgsmål med andre ord end teksten fandt den først på plads 21: `nomic-embed-text` er primært engelsk. `search_query:`/`search_document:`-præfikserne gjorde det værre for dansk (21 → 56), og HyDE (lad modellen skrive et hypotetisk svar og søg på det) hjalp kun lidt (21 → 14) for 2-4 sekunders ekstra ventetid, så ingen af delene bruges. En flersproget embedding-model (fx `bge-m3`) er næste skridt.

## RagChat
Systemprompten (`RagContextInstructions`) beder modellen svare på spørgsmålets sprog, kun ud fra konteksten, henvise med `[n]` og sige det ligeud, når konteksten ikke indeholder svaret. Hver del af konteksten er `[n] Source: <filnavn>` efterfulgt af chunkens tekst med dens overskriftssti. Søgningen bruger stadig kun den seneste `user`-besked.

Samtalen sendes med til modellen, men påvirker ikke søgningen. Et opfølgende spørgsmål, der kun giver mening med de tidligere beskeder ("og hvad med den anden?"), finder derfor sjældent de rigtige chunks. Brug `AgentChat` til det. Den omformulerer opgaven ud fra hele samtalen, før den søger.

## AgentChat (`KnowledgeBaseManager.Agent.cs`)
En research-agent, der kalder samme Ollama-model flere gange. Stateless som RagChat: kalderen sender hele samtalen hver gang. Trinnene:

1. **Hurtig søgning** på den seneste besked (4 uddrag). Uddragene vises til trin 2 og 3, så modellen ser, hvad og hvem dokumenterne handler om, og hvilket sprog og ordvalg de bruger. De tæller også med som fund.
2. **Vurdering** (JSON-skema): `task` (opgaven omformuleret med alt fra samtalen), `has_clear_goal` og `too_broad`. Agenten spørger tilbage (højst 3 spørgsmål) kun når målet mangler, eller opgaven er for bred. To konkrete egenskaber i stedet for ét "klar?" var nødvendigt: med ét samlet spørgsmål spurgte en 14B-model tilbage om helt klare spørgsmål. Instruksen forbyder spørgsmål om, hvilke dokumenter der skal bruges, om det dokumenterne selv svarer på, og om det uddragene gør klart. Modellen ser også dokumenternes filnavne (højst 50). `SkipClarification` springer spørgsmålene over.
3. **Søgeplan** (JSON-skema): `QueriesPerRound` (4) korte søgninger med dokumenternes ord og forskellige formuleringer, plus opgaven selv. Det opvejer embedding-modellens svaghed over for andre ord end dokumenternes: et hvorfor-spørgsmål, som RagChats ene søgning fandt på plads 21, fandt agenten.
4. **Søgning i runder:** hver søgning giver `ResultsPerQuery` (5) chunks; dubletter slås sammen (højeste score). Efter første runde vurderer modellen fundene (JSON: `enough` + nye søgninger for det, der mangler) op til `MaxSearchRounds` (2).
5. **Svar:** fra de `MaxSources` (12) mest relevante uddrag, nummereret `[n] Source: <fil>`. Kort direkte svar først, så detaljer i Markdown; hver påstand med `[n]`; uenighed og mangler nævnes. `num_ctx` 16384.

Svaret i API'et markerer, hvilke kilder svaret faktisk citerer (`Cited`). Kan modellens JSON ikke læses, falder trinnet tilbage til en simpel standard (opgaven = seneste besked, søgning på den), så agenten aldrig fejler på det.

**Målt på en skannet journal (qwen2.5:14b-instruct):** 7 af 7 vurderinger korrekte (3 klare spørgsmål undersøgt direkte, 2 vage opgaver gav spørgsmål), spørgsmål tilbage på 4-9 sekunder, et undersøgt svar på 25-45 sekunder.

## Markdown-version (`MarkdownFormatter` + én converter pr. filtype)
Laves af baggrundsbehandlingen sammen med den almindelige tekst og gemmes i `KnowledgeDocuments.MarkdownText`. Strukturen kommer fra selve filen, ikke fra en LLM, så teksten ændres aldrig, kun dens opsætning.

| Fil | Converter | Resultat |
|---|---|---|
| `.docx` | `DocxMarkdownConverter` | Overskrifter fra typografierne (fundet på det engelske typografinavn "heading N", så danske "Overskrift 1" virker). Har dokumentet en Title, bliver den `#`, og overskrifterne rykker ét niveau ned. Punkt- og nummererede lister (også indlejrede), tabeller (første række er overskrift), fed, kursiv og links. Indholdsfortegnelse, billeder, fodnoter, sidehoveder og sidefødder udelades. |
| PDF | `PdfMarkdownConverter` | Tekstlaget deles i blokke i læserækkefølge (PdfPig Docstrum + `UnsupervisedReadingOrderDetector`). Overskrifter: blokke med skrift mindst 15 % større end brødteksten (op til 3 linjer og 200 tegn). De tre største størrelser bliver `#`, `##` og `###`. En kort, fed linje i brødtekststørrelse bliver niveauet under. Lister fra punkttegn og tal. Sidetal og sidehoveder/-fødder, der går igen på mindst halvdelen af siderne (mindst 3), fjernes. OCR-tekst (skannede sider, billeder) kommer med som afsnit. Tabeller bliver til afsnit. |
| Billeder | `MarkdownFormatter.AppendPlainText` | OCR-teksten som afsnit, etiketter, felter og lister, uden gentagne sidehoveder/-fødder (se nedenfor). |
| `.md` | `TextMarkdownConverter` | Uændret. |
| `.csv`, `.tsv` | `TextMarkdownConverter` | Tabel. Skilletegnet (`;`, `,` eller tab) findes ud fra første linje. |
| `.json`, `.xml`, `.yaml`, `.html`, kildekode, `.log` … | `TextMarkdownConverter` | Kodeblok med sprog. |
| Anden tekst | `MarkdownFormatter.FromPlainText` | Afsnit (tomme linjer), lister (punkttegn og tal), felter (`Etiket: værdi` → `**Etiket:** værdi`) og etiketter (en kort linje med stort begyndelsesbogstav uden punktum, alene eller over længere tekst eller en ren værdi → **fed**). Linjer i et afsnit samles, og ord delt med bindestreg over to linjer samles igen. |

Al tekst fra filen escapes (`*`, `_`, `[`, `#` i starten af en linje …), så den vises præcis som skrevet.

**OCR-sider (`MarkdownFormatter.RemoveRepeatedPageLines`):** skannede sider gentager sidehoved og sidefod (udskriftsstempel, navn, afdeling, URL). Blandt hver sides første og sidste 3 linjer findes linjer, der går igen på mindst 30 % af siderne (mindst 3). De sammenlignes kun på bogstaver og med lighed (bigram-Dice ≥ 0,85), så datoer, sidetællere og OCR-støj ikke gør dem forskellige. De beholdes første gang og fjernes derefter. Sidetal fjernes altid. Kun Markdown påvirkes; den indekserede tekst er uændret.

**`MarkdownFormatter.Version`:** hæves, når en converter ændrer sin Markdown. Dokumenter med en ældre version laves igen i baggrunden.

### LLM-opsætning af OCR-tekst (`KnowledgeBaseManager.Markdown.cs`)
OCR-tekst har ingen struktur at bygge på, så strukturen giver kun afsnit. Kommer mindst `KnowledgeBase:Markdown:LlmMinOcrShare` (0,5) af et dokuments tekst fra OCR, og er `LlmModel` sat, får det `MarkdownSource = LlmPending`. Det er søgbart og har strukturens Markdown med det samme. `KnowledgeMarkdownWorker` lægger det derefter op med Ollama:

1. Strukturens Markdown (allerede uden gentagne sidehoveder, med etiketter og felter markeret) deles ved afsnit i dele på ca. `LlmChunkCharacters` (3000) tegn.
2. Hver del sendes til `LlmModel` med temperatur 0, `num_ctx` 8192 og en instruks om kun at ændre opsætningen: `##` for dokumentets dele eller indførsler (breve, notater, kapitler), `###` for afsnit i dem, aldrig dybere, samt lister, tabeller og samlede afsnit. Første del må lave titlen som `#`.
3. Svaret kontrolleres ord for ord mod delen (bogstaver og tal, uden forskel på store og små): mindst `LlmMinWordCoverage` (95 %) af ordene skal være med, og højst 5 % må være nye. Afvises delen, prøves den én gang til delt i to halvdele ved afsnittet nærmest midten (modellen udelader færre linjer i små dele), og hver halvdel bedømmes for sig. Det, der stadig afvises, får strukturens Markdown. Instruksen kræver udtrykkeligt, at alle linjer kommer med, også gentagne linjer, URL'er og koder, som modellen ellers er tilbøjelig til at droppe.
4. Når alle dele er færdige, indekseres dokumentet igen fra LLM'ens Markdown, før den gemmes som `Llm` (fejler det, gentages kun det sidste trin). Kladden og antallet af færdige dele gemmes efter hver del (`MarkdownLlmDraft`, `MarkdownLlmPartsDone`/`Total`). Efter en genstart fortsætter den ved næste del, hvis opdelingen giver samme antal dele; ellers starter den forfra. `ExportDocumentMarkdown` viser kilden i headeren `X-Markdown-Source` og fremskridtet i `X-Markdown-Llm-Progress` (fx `12/50`). Strukturens Markdown vises, indtil alle dele er færdige.
5. Er mindst én del lagt op af LLM'en, gemmes resultatet med kilden `Llm`. Ellers `Structure`.

Svarer Ollama med en fejl (fx at modellen mangler), springes dokumentet over (`Structure`). Kan Ollama ikke nås, bliver det i køen og prøves igen ved næste gennemløb. En timeout på én del giver strukturens Markdown for den del.

## Tekstudtræk fra billeder (`KnowledgeBaseManager.Extraction.cs`)
Hele billedet sendes til Tesseract med `--psm 3` og uden `--dpi`, så Tesseract bruger billedets egen opløsning eller estimerer den. En TIFF med flere sider giver ord fra alle sider, og hver side kvalitetstjekkes for sig (samme tjek som PDF'er, se nedenfor). Afvises en side, og fandt Tesseract ord, prøves hele billedet igen med `--psm 1` (orienteringsgenkendelse). Er der ingen brugbar tekst, får dokumentet status `Failed` med begrundelsen. Kræver `KnowledgeBase:Ocr:Enabled = true`.

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
- Upload svarer med det samme (`202`, status `Processing`). Udtræk, OCR og indeksering sker i `KnowledgeDocumentProcessingWorker`, ét dokument ad gangen, ældste først (siderne i en PDF OCR'es stadig parallelt). En skannet side tager typisk 2-4 sekunder, så store skannede PDF'er kan stå i kø et stykke tid.
- Tabellen `KnowledgeDocuments` er køen: dokumenter i `Processing` behandles. Lukkes appen midt i en behandling, forbliver dokumentet `Processing` og behandles forfra ved næste opstart. Uploads vækker workeren straks via `KnowledgeDocumentProcessingSignal`. `KnowledgeBase:WorkerIntervalSeconds` (60) er kun et fallback-tjek.
- Slettes et dokument eller dets gruppe, mens det behandles, kasseres resultatet (`MarkIndexedAsync`/`MarkFailedAsync` returnerer `null`).
- Kan Tesseract ikke køre, logges en advarsel, og tekstlaget bruges alene. Har PDF'en intet tekstlag, får dokumentet status `Failed` med Tesseracts fejl i `ErrorMessage`.
- Positioner fra PdfPig (0.1.16) er allerede drejet efter sidens `/Rotate` og flyttet til MediaBox'ens origo, mens `CropBox` står i rå PDF-koordinater. `DocumentTextExtractor.ToPdfPageDto` regner begge om til den viste side. Tjek det, hvis PdfPig opgraderes.
- Markdown er kun en pænere visning af den samme tekst og må aldrig få et upload til at fejle. Fejler en converter, logges en advarsel, og den almindelige tekst bruges som Markdown (`BuildMarkdown`). Fejler PdfPigs layoutanalyse på en side, bruges sidens tekst uden layout.
- LLM-opsætningen ændrer kun Markdown. Den indekserede tekst, chunks og vektorer er altid OCR-teksten.
- `KnowledgeMarkdownWorker` kører for sig selv, så en lang skanning aldrig holder nye uploads tilbage. Målt med `qwen2.5:14b-instruct`: ca. 20 sekunder pr. del (53 tokens/s), dvs. ca. 17 minutter for en skannet journal på 60 sider (50 dele). Ollama deler dog sin kapacitet mellem LLM-opsætning, embeddings og chat.
- Dokumenter, der er indekseret før Markdown fandtes, får den automatisk i baggrunden efter nye uploads. Det kræver et nyt udtræk (også OCR), men ingen ny embedding. Kan filen ikke læses igen, bruges den gemte tekst.
- En flersidet TIFF OCR'es i ét Tesseract-kald, så `Tesseract:TimeoutSeconds` (120) gælder hele filen. Store TIFF'er med mange sider kan kræve et højere loft.
- OCR-tærsklerne er kalibreret til Tesseract med `tessdata_best`. Hæves de, kommer der mindre støj med, men der mistes også flere rigtige ord.
- `DocumentTextExtractor`, `PdfPageRenderer`, `OcrTextBuilder` og `TextQuality` ligger her og ikke i `extensions/`, fordi kun knowledge base bruger dem.
- Skiftes embedding-model eller -dimension, skal `vec_chunks` genskabes, og alle dokumenter skal embeddes igen.
