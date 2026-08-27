using Microsoft.Extensions.AI;
using Microsoft.ML.Tokenizers;
using ZVec.Rag.Constants;
using ZVec.Rag.Options;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Resolves Tiktoken encodings for chunkers and context packing.
/// </summary>
public sealed class ZVecTokenizerResolver
{
    private readonly ZVecRagOptions _options;

    /// <summary>Initializes a new instance.</summary>
    public ZVecTokenizerResolver(ZVecRagOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Creates a tokenizer aligned with embedder metadata and options.</summary>
    public TiktokenTokenizer CreateTokenizer()
    {
        string encoding = ResolveEncodingName();
        return TiktokenTokenizer.CreateForEncoding(encoding);
    }

    private string ResolveEncodingName()
    {
        if (!string.IsNullOrWhiteSpace(_options.TokenizerEncoding))
        {
            return _options.TokenizerEncoding;
        }

        return ZVecRagConstants.Cl100kBaseEncoding;
    }
}
