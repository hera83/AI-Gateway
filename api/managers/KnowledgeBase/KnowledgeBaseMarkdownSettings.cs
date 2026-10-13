namespace api.Managers.KnowledgeBase;

// Bound from "KnowledgeBase:Markdown". The LLM only lays out text that came from OCR, which has no structure of
// its own; files with real structure (Word, PDFs with a text layer) keep the Markdown made from it.
public class KnowledgeBaseMarkdownSettings
{
    // Ollama chat model. Empty = no LLM, the Markdown is always made from the structure.
    public string LlmModel { get; set; } = string.Empty;

    // Share of a document's text (0-1) that must come from OCR before the LLM lays it out.
    public double LlmMinOcrShare { get; set; } = 0.5;

    // The text is sent in parts of about this size, split at paragraphs.
    public int LlmChunkCharacters { get; set; } = 3000;

    // Share of the part's words (0-1) the LLM must keep, and the most it may add as new words (1 - this).
    // Anything else means it rewrote the text, and the part keeps the Markdown made from the structure.
    public double LlmMinWordCoverage { get; set; } = 0.95;
}
