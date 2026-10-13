using System.Text.Json;
using api.Managers.KnowledgeBase.Dtos;
using api.Repositories.KnowledgeDocuments.Dtos;
using api.Services.Ollama.Dtos.Chat;
using api.Services.Ollama.Dtos.Common;

namespace api.Managers.KnowledgeBase;

// AgentChat: a research agent over the knowledge base. Unlike RagChat (one search, one answer) it
// 1. checks whether the task is clear enough to research, and otherwise asks questions back,
// 2. plans several searches with different wording,
// 3. reads what it found and searches again for what is missing (up to MaxSearchRounds),
// 4. writes a structured answer from the excerpts, citing them as [n].
// Steps 1-3 ask the model for JSON with a schema (Ollama's structured output), which a local model follows far more
// reliably than free-text instructions. Stateless like RagChat: the caller sends the whole conversation every turn.
public partial class KnowledgeBaseManager
{
    // Room for the excerpts (MaxSources × ChunkSizeCharacters), the conversation and the answer.
    private const int AgentContextTokens = 16384;

    // The file names the intake step sees, so it can judge whether the task fits the documents at all.
    private const int MaxDocumentNamesForAssessment = 50;

    // A quick search for the latest message before anything else: its excerpts show the intake and planning steps
    // what (and whom) the documents are about and which language and words they use, so the agent doesn't ask about
    // what the documents make clear, or plan searches for topics that aren't there.
    private const int ScoutResults = 4;
    private const int ScoutPreviewCharacters = 400;

    private static readonly JsonSerializerOptions AgentJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonElement AssessmentSchema = JsonDocument.Parse(
        """
        {"type":"object","properties":{"task":{"type":"string"},"has_clear_goal":{"type":"boolean"},"too_broad":{"type":"boolean"},"questions":{"type":"array","items":{"type":"string"}}},"required":["task","has_clear_goal","too_broad","questions"]}
        """).RootElement.Clone();

    private static readonly JsonElement SearchPlanSchema = JsonDocument.Parse(
        """
        {"type":"object","properties":{"enough":{"type":"boolean"},"queries":{"type":"array","items":{"type":"string"}}},"required":["enough","queries"]}
        """).RootElement.Clone();

    private const string LanguageRule =
        "Write every text you produce in the language of the user's latest message (if the user writes Danish, write Danish).";

    private const string AssessmentInstructions =
        """
        You are the intake step of a research assistant that answers questions from a private collection of documents (a knowledge base).
        Read the conversation and judge the user's request:
        - "task": the research task restated in one or two precise sentences, including everything the user has explained in the conversation.
        - "has_clear_goal": true when it is clear what the user wants to find out. A question (why, what, when, how, who, how much …) always has a clear goal, also when it is short.
        - "too_broad": true only when the request covers so much that one answer can't do it justice, for example "investigate the treatment" without saying which aspect or period. A specific question is never too broad. If the user has answered your earlier questions, it is no longer too broad.
        - "questions": only when has_clear_goal is false or too_broad is true: at most 3 short, concrete questions that would make it researchable. Otherwise an empty list.
        Never ask which documents, files or sources to use (the user can only research these documents), never ask for what the documents themselves will answer (that is the research), and never ask about what the excerpts below make clear (for example which person or family, when the documents are about one person).
        Answer as JSON: {"task": "...", "has_clear_goal": true|false, "too_broad": true|false, "questions": ["..."]}.
        """;

    private const string SearchPlanInstructions =
        """
        You plan searches for a research assistant. The search is semantic (vector embeddings) over excerpts of the documents, so a query finds excerpts with similar wording and meaning.
        Write {0} short search queries (3-10 words each) that together find the excerpts needed for the research task.
        - Use the words the documents themselves use (see the example excerpts), not the wording of the question. Vary the wording and cover different aspects of the task.
        - Write the queries in the language of the documents' excerpts, and stay with what the documents are about.
        Answer as JSON: {{"enough": false, "queries": ["..."]}}.
        """;

    private const string ReviewInstructions =
        """
        You review the excerpts a research assistant has found so far for a research task.
        Decide whether they are enough to answer the task well. If not, write up to {0} new short search queries (3-10 words, in the language of the task) for what is still missing; use other words than the earlier queries.
        Answer as JSON: {{"enough": true|false, "queries": ["..."]}}. When enough is true, queries is empty.
        """;

    private const string AnswerInstructions =
        """
        You are a research assistant. Write a thorough, well-structured answer to the research task using only the numbered excerpts from the user's documents below.
        - Start with a short, direct answer, then the details, using Markdown headings, lists or a table where it helps.
        - Cite the excerpts you use by their number right after the statement they support, like [1] or [2][5]. Every fact needs a citation.
        - Each excerpt starts with its source file; its text may start with the headings of the section it comes from (for example the note or date it belongs to).
        - If excerpts disagree, say so. If they don't fully answer the task, say clearly what is missing. Never add facts that are not in the excerpts.
        """;

    // null = the group doesn't exist (or belongs to another key).
    public async Task<AgentChatResultDto?> AgentChatAsync(AgentChatDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.KnowledgeGroupId is { } groupId && await knowledgeGroupRepository.GetByIdAsync(dto.ApiKeyId, groupId, cancellationToken) is null)
        {
            return null;
        }

        // AgentChatKnowledgeBaseRequestDto validates that a user message exists, so this is unexpected here.
        var lastUserMessage = dto.Messages.LastOrDefault(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Messages must include at least one message with role 'user'.");
        var conversation = dto.Messages
            .Where(message => !string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
            .Select(message => new MessageDto { Role = message.Role, Content = message.Content })
            .ToList();

        var settings = options.Value.Agent;
        var scout = await FindMatchesAsync(dto.ApiKeyId, lastUserMessage.Content, ScoutResults, dto.KnowledgeGroupId, cancellationToken);
        var scoutPreviews = string.Join("\n\n", scout.Select(match =>
            $"Source: {match.OriginalFileName}\n{(match.Text.Length > ScoutPreviewCharacters ? match.Text[..ScoutPreviewCharacters] + " …" : match.Text)}"));

        // 1. Is the task clear enough? Without an answer from the model, research the latest message as it is.
        var assessment = await AssessTaskAsync(dto, conversation, scoutPreviews, cancellationToken)
            ?? new AgentAssessmentDto { HasClearGoal = true, Task = lastUserMessage.Content };
        var task = string.IsNullOrWhiteSpace(assessment.Task) ? lastUserMessage.Content : assessment.Task;
        var questions = assessment.Questions.Where(question => !string.IsNullOrWhiteSpace(question)).Take(3).ToList();
        if (!dto.SkipClarification && !assessment.IsReady && questions.Count > 0)
        {
            logger.LogInformation("AgentChat asks {QuestionCount} clarifying question(s)", questions.Count);
            return new AgentChatResultDto
            {
                Outcome = AgentChatOutcome.NeedsClarification,
                Model = dto.Model,
                Task = task,
                Questions = questions,
                Message = string.Join("\n", questions.Select(question => $"- {question}"))
            };
        }

        // 2-3. Search, read, search again for what's missing. The quick search's excerpts count as found already.
        var queries = new List<string> { lastUserMessage.Content };
        var found = scout.ToDictionary(match => match.ChunkId);
        var nextQueries = (await AskForJsonAsync<AgentSearchPlanDto>(
                dto.Model,
                string.Format(SearchPlanInstructions, settings.QueriesPerRound),
                [new MessageDto { Role = "user", Content = $"Research task: {task}\n\nExample excerpts from the documents:\n\n{scoutPreviews}" }],
                SearchPlanSchema,
                cancellationToken))?.Queries
            ?? [];
        nextQueries = CleanQueries(nextQueries.Prepend(task), queries, settings.QueriesPerRound + 1);

        for (var round = 1; round <= Math.Max(1, settings.MaxSearchRounds) && nextQueries.Count > 0; round++)
        {
            foreach (var query in nextQueries)
            {
                queries.Add(query);
                foreach (var match in await FindMatchesAsync(dto.ApiKeyId, query, settings.ResultsPerQuery, dto.KnowledgeGroupId, cancellationToken))
                {
                    if (!found.TryGetValue(match.ChunkId, out var existing) || existing.Score < match.Score)
                    {
                        found[match.ChunkId] = match;
                    }
                }
            }

            if (round == settings.MaxSearchRounds || found.Count == 0)
            {
                break;
            }

            var review = await AskForJsonAsync<AgentSearchPlanDto>(
                dto.Model,
                string.Format(ReviewInstructions, settings.QueriesPerRound),
                [new MessageDto { Role = "user", Content = $"Research task: {task}\n\nQueries so far:\n{string.Join("\n", queries)}\n\nExcerpts found:\n\n{FormatExcerpts(SelectSources(found, settings.MaxSources))}" }],
                SearchPlanSchema,
                cancellationToken);
            nextQueries = review is null || review.Enough ? [] : CleanQueries(review.Queries, queries, settings.QueriesPerRound);
        }

        // 4. The answer, from the most relevant excerpts.
        var sources = SelectSources(found, settings.MaxSources);
        var answerMessages = new List<MessageDto>
        {
            new()
            {
                Role = "system",
                Content = $"{AnswerInstructions}\n{LanguageRule}\n\nResearch task: {task}\n\n" +
                          (sources.Count > 0 ? $"Excerpts:\n\n{FormatExcerpts(sources)}" : "No excerpts were found in the documents. Say so, and suggest how the task could be put differently.")
            }
        };
        answerMessages.AddRange(conversation);
        var chat = await ollamaService.ChatAsync(new ChatRequestDto
        {
            Model = dto.Model,
            Messages = answerMessages,
            Options = new OptionsDto { NumCtx = AgentContextTokens },
            Stream = false
        }, cancellationToken);

        logger.LogInformation(
            "AgentChat researched a task with {QueryCount} queries, {FoundCount} excerpts found, {SourceCount} used",
            queries.Count, found.Count, sources.Count);

        return new AgentChatResultDto
        {
            Outcome = AgentChatOutcome.Answered,
            Model = chat.Model,
            Task = task,
            Message = ThinkingRegex().Replace(chat.Message.Content, string.Empty).Trim(),
            Queries = queries,
            Sources = sources
        };
    }

    private async Task<AgentAssessmentDto?> AssessTaskAsync(AgentChatDto dto, List<MessageDto> conversation, string scoutPreviews, CancellationToken cancellationToken)
    {
        if (dto.SkipClarification && conversation.Count(message => message.Role == "user") == 1)
        {
            // Nothing to restate: the task is the one message.
            return null;
        }

        var documentNames = (await knowledgeDocumentRepository.GetAllAsync(dto.ApiKeyId, dto.KnowledgeGroupId, cancellationToken))
            .Where(document => document.Status == "Indexed")
            .Select(document => document.OriginalFileName)
            .Take(MaxDocumentNamesForAssessment)
            .ToList();
        var system = $"{AssessmentInstructions}\n{LanguageRule}\n\nThe documents ({documentNames.Count}{(documentNames.Count == MaxDocumentNamesForAssessment ? "+" : string.Empty)}): " +
                     (documentNames.Count == 0 ? "none" : string.Join(", ", documentNames)) +
                     (scoutPreviews.Length == 0 ? string.Empty : $"\n\nExcerpts a quick search found for the latest message (only to show what the documents are about):\n\n{scoutPreviews}");
        return await AskForJsonAsync<AgentAssessmentDto>(dto.Model, system, conversation, AssessmentSchema, cancellationToken);
    }

    // null when the model's answer isn't valid JSON; the caller then falls back to a plain default.
    private async Task<T?> AskForJsonAsync<T>(string model, string system, IEnumerable<MessageDto> messages, JsonElement schema, CancellationToken cancellationToken)
        where T : class
    {
        var chat = await ollamaService.ChatAsync(new ChatRequestDto
        {
            Model = model,
            Messages = [new MessageDto { Role = "system", Content = system }, .. messages],
            Format = schema,
            Options = new OptionsDto { Temperature = 0, NumCtx = AgentContextTokens },
            Stream = false
        }, cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<T>(ThinkingRegex().Replace(chat.Message.Content, string.Empty), AgentJsonOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "AgentChat: the model's answer for {Step} was not valid JSON", typeof(T).Name);
            return null;
        }
    }

    // Distinct, non-empty queries not run before, at most max.
    private static List<string> CleanQueries(IEnumerable<string> candidates, List<string> alreadyRun, int max) =>
        candidates
            .Select(query => query.Trim())
            .Where(query => query.Length > 0 && !alreadyRun.Contains(query, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();

    private static List<KnowledgeChunkMatchDto> SelectSources(Dictionary<Guid, KnowledgeChunkMatchDto> found, int max) =>
        found.Values.OrderByDescending(match => match.Score).Take(Math.Max(1, max)).ToList();

    private static string FormatExcerpts(List<KnowledgeChunkMatchDto> sources) =>
        string.Join("\n\n", sources.Select((source, index) => $"[{index + 1}] Source: {source.OriginalFileName}\n{source.Text}"));
}
