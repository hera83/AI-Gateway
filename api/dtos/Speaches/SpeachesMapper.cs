using SvcAudio = api.Services.Speaches.Dtos.Audio;
using SvcChat = api.Services.Speaches.Dtos.Chat;
using SvcModels = api.Services.Speaches.Dtos.Models;

namespace api.Dtos.Speaches;

public static class SpeachesMapper
{
    // Chat completions

    public static SvcChat.ChatCompletionRequestDto ToChatCompletionRequestDto(this ChatCompletionsSpeachesRequestDto request) => new()
    {
        Model = request.Model,
        Messages = request.Messages.Select(ToChatCompletionMessageDto).ToList(),
        Temperature = request.Temperature,
        TopP = request.TopP,
        MaxCompletionTokens = request.MaxCompletionTokens,
        N = request.N,
        Stop = request.Stop,
        PresencePenalty = request.PresencePenalty,
        FrequencyPenalty = request.FrequencyPenalty,
        Seed = request.Seed,
        User = request.User,
        Tools = request.Tools,
        ToolChoice = request.ToolChoice,
        ResponseFormat = request.ResponseFormat,
        Logprobs = request.Logprobs,
        TopLogprobs = request.TopLogprobs
    };

    public static SvcChat.ChatCompletionMessageDto ToChatCompletionMessageDto(this ChatCompletionMessageDto message) => new()
    {
        Role = message.Role,
        Content = message.Content,
        Name = message.Name,
        ToolCallId = message.ToolCallId,
        ToolCalls = message.ToolCalls,
        Refusal = message.Refusal
    };

    public static ChatCompletionsSpeachesResponseDto ToChatCompletionsSpeachesResponseDto(this SvcChat.ChatCompletionResponseDto response) => new()
    {
        Id = response.Id,
        Object = response.Object,
        Created = response.Created,
        Model = response.Model,
        Choices = response.Choices.Select(ToChatCompletionChoiceDto).ToList(),
        Usage = response.Usage?.ToChatCompletionUsageDto(),
        SystemFingerprint = response.SystemFingerprint,
        ServiceTier = response.ServiceTier
    };

    public static ChatCompletionChoiceDto ToChatCompletionChoiceDto(this SvcChat.ChatCompletionChoiceDto choice) => new()
    {
        Index = choice.Index,
        Message = choice.Message.ToChatCompletionResponseMessageDto(),
        FinishReason = choice.FinishReason,
        Logprobs = choice.Logprobs
    };

    public static ChatCompletionResponseMessageDto ToChatCompletionResponseMessageDto(this SvcChat.ChatCompletionResponseMessageDto message) => new()
    {
        Role = message.Role,
        Content = message.Content,
        Refusal = message.Refusal,
        ToolCalls = message.ToolCalls,
        Audio = message.Audio,
        Annotations = message.Annotations
    };

    public static ChatCompletionUsageDto ToChatCompletionUsageDto(this SvcChat.ChatCompletionUsageDto usage) => new()
    {
        PromptTokens = usage.PromptTokens,
        CompletionTokens = usage.CompletionTokens,
        TotalTokens = usage.TotalTokens,
        PromptTokensDetails = usage.PromptTokensDetails,
        CompletionTokensDetails = usage.CompletionTokensDetails
    };

    // Transcribe / translate

    public static SvcAudio.TranscriptionRequestDto ToTranscriptionRequestDto(this TranscribeSpeachesRequestDto request) => new()
    {
        Model = request.Model,
        File = request.File.OpenReadStream(),
        FileName = request.File.FileName,
        ContentType = request.File.ContentType,
        Language = request.Language,
        Prompt = request.Prompt,
        Temperature = request.Temperature,
        TimestampGranularities = request.TimestampGranularities,
        Hotwords = request.Hotwords,
        WithoutTimestamps = request.WithoutTimestamps
    };

    public static TranscribeSpeachesResponseDto ToTranscribeSpeachesResponseDto(this SvcAudio.TranscriptionResponseDto response) => new()
    {
        Text = response.Text,
        Language = response.Language,
        Duration = response.Duration,
        Segments = response.Segments?.Select(ToTranscriptionSegmentDto).ToList(),
        Words = response.Words?.Select(ToTranscriptionWordDto).ToList()
    };

    public static TranscriptionSegmentDto ToTranscriptionSegmentDto(this SvcAudio.TranscriptionSegmentDto segment) => new()
    {
        Id = segment.Id,
        Start = segment.Start,
        End = segment.End,
        Text = segment.Text,
        Tokens = segment.Tokens,
        Temperature = segment.Temperature,
        AvgLogprob = segment.AvgLogprob,
        CompressionRatio = segment.CompressionRatio,
        NoSpeechProb = segment.NoSpeechProb,
        Seek = segment.Seek
    };

    public static TranscriptionWordDto ToTranscriptionWordDto(this SvcAudio.TranscriptionWordDto word) => new()
    {
        Word = word.Word,
        Start = word.Start,
        End = word.End
    };

    public static SvcAudio.TranslationRequestDto ToTranslationRequestDto(this TranslateSpeachesRequestDto request) => new()
    {
        Model = request.Model,
        File = request.File.OpenReadStream(),
        FileName = request.File.FileName,
        ContentType = request.File.ContentType,
        Prompt = request.Prompt,
        Temperature = request.Temperature
    };

    public static TranslateSpeachesResponseDto ToTranslateSpeachesResponseDto(this SvcAudio.TranslationResponseDto response) => new()
    {
        Text = response.Text,
        Language = response.Language,
        Duration = response.Duration,
        Segments = response.Segments?.Select(ToTranscriptionSegmentDto).ToList()
    };

    // Speech synthesis / embedding / timestamps / diarization

    public static SvcAudio.SynthesizeRequestDto ToSynthesizeRequestDto(this SynthesizeSpeechSpeachesRequestDto request) => new()
    {
        Model = request.Model,
        Input = request.Input,
        Voice = request.Voice,
        ResponseFormat = request.ResponseFormat,
        Speed = request.Speed,
        SampleRate = request.SampleRate
    };

    public static SvcAudio.SpeechEmbeddingRequestDto ToSpeechEmbeddingRequestDto(this CreateSpeechEmbeddingSpeachesRequestDto request) => new()
    {
        Model = request.Model,
        File = request.File.OpenReadStream(),
        FileName = request.File.FileName,
        ContentType = request.File.ContentType
    };

    public static CreateSpeechEmbeddingSpeachesResponseDto ToCreateSpeechEmbeddingSpeachesResponseDto(this SvcAudio.SpeechEmbeddingResponseDto response) => new()
    {
        Object = response.Object,
        Data = response.Data.Select(ToEmbeddingObjectDto).ToList(),
        Model = response.Model,
        Usage = new EmbeddingUsageDto
        {
            PromptTokens = response.Usage.PromptTokens,
            TotalTokens = response.Usage.TotalTokens
        }
    };

    public static EmbeddingObjectDto ToEmbeddingObjectDto(this SvcAudio.EmbeddingObjectDto embedding) => new()
    {
        Object = embedding.Object,
        Index = embedding.Index,
        Embedding = embedding.Embedding
    };

    public static SvcAudio.SpeechTimestampsRequestDto ToSpeechTimestampsRequestDto(this DetectSpeechTimestampsSpeachesRequestDto request) => new()
    {
        File = request.File.OpenReadStream(),
        FileName = request.File.FileName,
        ContentType = request.File.ContentType,
        Model = request.Model,
        Threshold = request.Threshold,
        NegThreshold = request.NegThreshold,
        MinSpeechDurationMs = request.MinSpeechDurationMs,
        MaxSpeechDurationS = request.MaxSpeechDurationS,
        MinSilenceDurationMs = request.MinSilenceDurationMs,
        SpeechPadMs = request.SpeechPadMs
    };

    public static DetectSpeechTimestampsSpeachesResponseDto ToDetectSpeechTimestampsSpeachesResponseDto(this SvcAudio.SpeechTimestampDto timestamp) => new()
    {
        Start = timestamp.Start,
        End = timestamp.End
    };

    public static SvcAudio.DiarizationRequestDto ToDiarizationRequestDto(this DiarizeSpeachesRequestDto request) => new()
    {
        File = request.File.OpenReadStream(),
        FileName = request.File.FileName,
        ContentType = request.File.ContentType,
        KnownSpeakerNames = request.KnownSpeakerNames,
        KnownSpeakerReferences = request.KnownSpeakerReferences
    };

    public static DiarizeSpeachesResponseDto ToDiarizeSpeachesResponseDto(this SvcAudio.DiarizationResponseDto response) => new()
    {
        Duration = response.Duration,
        Segments = response.Segments.Select(ToDiarizationSegmentDto).ToList()
    };

    public static DiarizationSegmentDto ToDiarizationSegmentDto(this SvcAudio.DiarizationSegmentDto segment) => new()
    {
        Start = segment.Start,
        End = segment.End,
        Speaker = segment.Speaker
    };

    // Models / registry / ps

    public static ModelDto ToModelDto(this SvcModels.ModelDto model) => new()
    {
        Id = model.Id,
        Created = model.Created,
        Object = model.Object,
        OwnedBy = model.OwnedBy,
        Language = model.Language,
        Task = model.Task
    };

    public static ListModelsSpeachesResponseDto ToListModelsSpeachesResponseDto(this SvcModels.ListModelsResponseDto response) => new()
    {
        Data = response.Data.Select(ToModelDto).ToList(),
        Object = response.Object
    };

    public static ListVoicesSpeachesResponseDto ToListVoicesSpeachesResponseDto(this SvcModels.ListModelsResponseDto response) => new()
    {
        Data = response.Data.Select(ToModelDto).ToList(),
        Object = response.Object
    };

    public static GetRegistrySpeachesResponseDto ToGetRegistrySpeachesResponseDto(this SvcModels.ListModelsResponseDto response) => new()
    {
        Data = response.Data.Select(ToModelDto).ToList(),
        Object = response.Object
    };

    public static ListAudioModelsSpeachesResponseDto ToListAudioModelsSpeachesResponseDto(this SvcModels.ListAudioModelsResponseDto response) => new()
    {
        Models = response.Models.Select(ToModelDto).ToList(),
        Object = response.Object
    };

    public static GetModelSpeachesResponseDto ToGetModelSpeachesResponseDto(this SvcModels.ModelDto model) => new()
    {
        Id = model.Id,
        Created = model.Created,
        Object = model.Object,
        OwnedBy = model.OwnedBy,
        Language = model.Language,
        Task = model.Task
    };

    public static ListRunningModelsSpeachesResponseDto ToListRunningModelsSpeachesResponseDto(this SvcModels.RunningModelsResponseDto response) => new()
    {
        Models = response.Models
    };

    public static LoadModelSpeachesResponseDto ToLoadModelSpeachesResponseDto(this SvcModels.MessageResponseDto response) => new()
    {
        Message = response.Message
    };

    public static StopModelSpeachesResponseDto ToStopModelSpeachesResponseDto(this SvcModels.MessageResponseDto response) => new()
    {
        Message = response.Message
    };
}
