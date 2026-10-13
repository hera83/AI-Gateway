namespace api.Data.Models;

// How a knowledge document's Markdown was made.
public enum KnowledgeMarkdownSource
{
    // From the file's own structure (headings, lists, tables).
    Structure,

    // From the structure for now; mostly OCR text, so an LLM will lay it out better.
    LlmPending,

    // Laid out by an LLM (Ollama) and checked word for word against the extracted text.
    Llm
}
