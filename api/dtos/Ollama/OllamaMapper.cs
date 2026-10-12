using api.Services.Ollama.Dtos.Chat;
using api.Services.Ollama.Dtos.Embed;
using api.Services.Ollama.Dtos.Generate;
using api.Services.Ollama.Dtos.Version;
using SvcCommon = api.Services.Ollama.Dtos.Common;
using SvcModels = api.Services.Ollama.Dtos.Models;

namespace api.Dtos.Ollama;

public static class OllamaMapper
{
    // Generate

    public static GenerateRequestDto ToGenerateRequestDto(this GenerateOllamaRequestDto request) => new()
    {
        Model = request.Model,
        Prompt = request.Prompt,
        Suffix = request.Suffix,
        Images = request.Images,
        Format = request.Format,
        Options = request.Options?.ToOptionsDto(),
        System = request.System,
        Template = request.Template,
        Context = request.Context,
        Raw = request.Raw,
        KeepAlive = request.KeepAlive
    };

    public static GenerateOllamaResponseDto ToGenerateOllamaResponseDto(this GenerateResponseDto response) => new()
    {
        Model = response.Model,
        CreatedAt = response.CreatedAt,
        Response = response.Response,
        Done = response.Done,
        DoneReason = response.DoneReason,
        Context = response.Context,
        TotalDuration = response.TotalDuration,
        LoadDuration = response.LoadDuration,
        PromptEvalCount = response.PromptEvalCount,
        PromptEvalDuration = response.PromptEvalDuration,
        EvalCount = response.EvalCount,
        EvalDuration = response.EvalDuration
    };

    // Chat

    public static ChatRequestDto ToChatRequestDto(this ChatOllamaRequestDto request) => new()
    {
        Model = request.Model,
        Messages = request.Messages.Select(ToMessageDto).ToList(),
        Tools = request.Tools?.Select(ToToolDto).ToList(),
        Format = request.Format,
        Options = request.Options?.ToOptionsDto(),
        KeepAlive = request.KeepAlive
    };

    public static ChatOllamaResponseDto ToChatOllamaResponseDto(this ChatResponseDto response) => new()
    {
        Model = response.Model,
        CreatedAt = response.CreatedAt,
        Message = response.Message.ToMessageDto(),
        Done = response.Done,
        DoneReason = response.DoneReason,
        TotalDuration = response.TotalDuration,
        LoadDuration = response.LoadDuration,
        PromptEvalCount = response.PromptEvalCount,
        PromptEvalDuration = response.PromptEvalDuration,
        EvalCount = response.EvalCount,
        EvalDuration = response.EvalDuration
    };

    // Embed

    public static EmbedRequestDto ToEmbedRequestDto(this EmbedOllamaRequestDto request) => new()
    {
        Model = request.Model,
        Input = request.Input,
        Options = request.Options?.ToOptionsDto(),
        KeepAlive = request.KeepAlive
    };

    public static EmbedOllamaResponseDto ToEmbedOllamaResponseDto(this EmbedResponseDto response) => new()
    {
        Model = response.Model,
        Embeddings = response.Embeddings,
        TotalDuration = response.TotalDuration,
        LoadDuration = response.LoadDuration,
        PromptEvalCount = response.PromptEvalCount
    };

    // Models

    public static ListModelsOllamaResponseDto ToListModelsOllamaResponseDto(this SvcModels.ListModelsResponseDto response) => new()
    {
        Models = response.Models.Select(ToModelSummaryDto).ToList()
    };

    public static ListRunningModelsOllamaResponseDto ToListRunningModelsOllamaResponseDto(this SvcModels.ListRunningModelsResponseDto response) => new()
    {
        Models = response.Models.Select(ToRunningModelDto).ToList()
    };

    public static SvcModels.ShowModelRequestDto ToShowModelRequestDto(this ShowModelOllamaRequestDto request) => new()
    {
        Model = request.Model
    };

    public static ShowModelOllamaResponseDto ToShowModelOllamaResponseDto(this SvcModels.ShowModelResponseDto response) => new()
    {
        License = response.License,
        Modelfile = response.Modelfile,
        Parameters = response.Parameters,
        Template = response.Template,
        Details = response.Details?.ToModelDetailsDto(),
        ModelInfo = response.ModelInfo,
        Capabilities = response.Capabilities
    };

    public static SvcModels.CopyModelRequestDto ToCopyModelRequestDto(this CopyModelOllamaRequestDto request) => new()
    {
        Source = request.Source,
        Destination = request.Destination
    };

    public static SvcModels.DeleteModelRequestDto ToDeleteModelRequestDto(this DeleteModelOllamaRequestDto request) => new()
    {
        Model = request.Model
    };

    public static SvcModels.PullModelRequestDto ToPullModelRequestDto(this PullModelOllamaRequestDto request) => new()
    {
        Model = request.Model,
        Insecure = request.Insecure
    };

    public static PullModelOllamaResponseDto ToPullModelOllamaResponseDto(this SvcModels.PullModelResponseDto response) => new()
    {
        Status = response.Status,
        Error = response.Error
    };

    public static SvcModels.PushModelRequestDto ToPushModelRequestDto(this PushModelOllamaRequestDto request) => new()
    {
        Model = request.Model,
        Insecure = request.Insecure
    };

    public static PushModelOllamaResponseDto ToPushModelOllamaResponseDto(this SvcModels.PushModelResponseDto response) => new()
    {
        Status = response.Status,
        Error = response.Error
    };

    public static SvcModels.CreateModelRequestDto ToCreateModelRequestDto(this CreateModelOllamaRequestDto request) => new()
    {
        Model = request.Model,
        From = request.From,
        Files = request.Files,
        Adapters = request.Adapters,
        Template = request.Template,
        License = request.License,
        System = request.System,
        Parameters = request.Parameters,
        Messages = request.Messages?.Select(ToMessageDto).ToList(),
        Quantize = request.Quantize
    };

    public static CreateModelOllamaResponseDto ToCreateModelOllamaResponseDto(this SvcModels.CreateModelResponseDto response) => new()
    {
        Status = response.Status,
        Error = response.Error
    };

    // Version

    public static GetVersionOllamaResponseDto ToGetVersionOllamaResponseDto(this VersionResponseDto response) => new()
    {
        Version = response.Version
    };

    // Shared sub-objects

    public static SvcCommon.OptionsDto ToOptionsDto(this OptionsDto options) => new()
    {
        NumKeep = options.NumKeep,
        Seed = options.Seed,
        NumPredict = options.NumPredict,
        TopK = options.TopK,
        TopP = options.TopP,
        MinP = options.MinP,
        TypicalP = options.TypicalP,
        RepeatLastN = options.RepeatLastN,
        Temperature = options.Temperature,
        RepeatPenalty = options.RepeatPenalty,
        PresencePenalty = options.PresencePenalty,
        FrequencyPenalty = options.FrequencyPenalty,
        Mirostat = options.Mirostat,
        MirostatTau = options.MirostatTau,
        MirostatEta = options.MirostatEta,
        PenalizeNewline = options.PenalizeNewline,
        Stop = options.Stop,
        Numa = options.Numa,
        NumCtx = options.NumCtx,
        NumBatch = options.NumBatch,
        NumGpu = options.NumGpu,
        MainGpu = options.MainGpu,
        LowVram = options.LowVram,
        VocabOnly = options.VocabOnly,
        UseMmap = options.UseMmap,
        UseMlock = options.UseMlock,
        NumThread = options.NumThread
    };

    public static SvcCommon.MessageDto ToMessageDto(this MessageDto message) => new()
    {
        Role = message.Role,
        Content = message.Content,
        Images = message.Images,
        ToolCalls = message.ToolCalls?.Select(ToToolCallDto).ToList()
    };

    public static MessageDto ToMessageDto(this SvcCommon.MessageDto message) => new()
    {
        Role = message.Role,
        Content = message.Content,
        Images = message.Images,
        ToolCalls = message.ToolCalls?.Select(ToToolCallDto).ToList()
    };

    public static SvcCommon.ToolCallDto ToToolCallDto(this ToolCallDto toolCall) => new()
    {
        Function = new SvcCommon.ToolCallFunctionDto
        {
            Name = toolCall.Function.Name,
            Arguments = toolCall.Function.Arguments
        }
    };

    public static ToolCallDto ToToolCallDto(this SvcCommon.ToolCallDto toolCall) => new()
    {
        Function = new ToolCallFunctionDto
        {
            Name = toolCall.Function.Name,
            Arguments = toolCall.Function.Arguments
        }
    };

    public static SvcCommon.ToolDto ToToolDto(this ToolDto tool) => new()
    {
        Type = tool.Type,
        Function = new SvcCommon.ToolFunctionDto
        {
            Name = tool.Function.Name,
            Description = tool.Function.Description,
            Parameters = tool.Function.Parameters
        }
    };

    public static ModelDetailsDto ToModelDetailsDto(this SvcModels.ModelDetailsDto details) => new()
    {
        ParentModel = details.ParentModel,
        Format = details.Format,
        Family = details.Family,
        Families = details.Families,
        ParameterSize = details.ParameterSize,
        QuantizationLevel = details.QuantizationLevel
    };

    public static ModelSummaryDto ToModelSummaryDto(this SvcModels.ModelSummaryDto model) => new()
    {
        Name = model.Name,
        Model = model.Model,
        ModifiedAt = model.ModifiedAt,
        Size = model.Size,
        Digest = model.Digest,
        Details = model.Details?.ToModelDetailsDto()
    };

    public static RunningModelDto ToRunningModelDto(this SvcModels.RunningModelDto model) => new()
    {
        Name = model.Name,
        Model = model.Model,
        Size = model.Size,
        Digest = model.Digest,
        Details = model.Details?.ToModelDetailsDto(),
        ExpiresAt = model.ExpiresAt,
        SizeVram = model.SizeVram
    };
}
