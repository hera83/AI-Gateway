namespace api.Services.Ollama.Dtos.Common;

public class OptionsDto
{
    public int? NumKeep { get; set; }

    public int? Seed { get; set; }

    public int? NumPredict { get; set; }

    public int? TopK { get; set; }

    public double? TopP { get; set; }

    public double? MinP { get; set; }

    public double? TypicalP { get; set; }

    public int? RepeatLastN { get; set; }

    public double? Temperature { get; set; }

    public double? RepeatPenalty { get; set; }

    public double? PresencePenalty { get; set; }

    public double? FrequencyPenalty { get; set; }

    public int? Mirostat { get; set; }

    public double? MirostatTau { get; set; }

    public double? MirostatEta { get; set; }

    public bool? PenalizeNewline { get; set; }

    public List<string>? Stop { get; set; }

    public bool? Numa { get; set; }

    public int? NumCtx { get; set; }

    public int? NumBatch { get; set; }

    public int? NumGpu { get; set; }

    public int? MainGpu { get; set; }

    public bool? LowVram { get; set; }

    public bool? VocabOnly { get; set; }

    public bool? UseMmap { get; set; }

    public bool? UseMlock { get; set; }

    public int? NumThread { get; set; }
}
