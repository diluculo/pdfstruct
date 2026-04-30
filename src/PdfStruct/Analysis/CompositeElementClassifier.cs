// Copyright (c) Jong Hyun Kim. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using PdfStruct.Models;

namespace PdfStruct.Analysis;

/// <summary>
/// Combines multiple <see cref="IElementClassifier"/>s by running each on
/// the same input and, per block, taking the first classifier that
/// produces a specific element type (anything other than a generic
/// <see cref="ParagraphElement"/>). Falls back to the first classifier's
/// paragraph when no inner classifier produces a specific type.
/// </summary>
/// <remarks>
/// This lets pattern- or domain-specific classifiers (for example a
/// <see cref="RegexHeadingClassifier"/> wired with corpus-specific
/// patterns) take precedence over a generic font-based classifier when
/// they recognize their input, while still letting the font-based
/// classifier handle the document title and any other elements the
/// upstream classifier does not flag.
/// </remarks>
public sealed class CompositeElementClassifier : IElementClassifier
{
    private readonly IElementClassifier[] _classifiers;

    /// <summary>
    /// Initializes a composite that delegates to the supplied classifiers
    /// in order. Earlier classifiers take precedence over later ones for
    /// any block they recognize as something other than a plain paragraph.
    /// </summary>
    public CompositeElementClassifier(params IElementClassifier[] classifiers)
    {
        _classifiers = classifiers ?? throw new ArgumentNullException(nameof(classifiers));
    }

    /// <inheritdoc />
    public IReadOnlyList<ContentElement> Classify(
        IReadOnlyList<TextBlock> blocks, int pageNumber, ref int startId)
    {
        if (_classifiers.Length == 0) return [];
        if (_classifiers.Length == 1)
            return _classifiers[0].Classify(blocks, pageNumber, ref startId);

        var perClassifier = new IReadOnlyList<ContentElement>[_classifiers.Length];
        for (var i = 0; i < _classifiers.Length; i++)
        {
            var throwaway = 0;
            perClassifier[i] = _classifiers[i].Classify(blocks, pageNumber, ref throwaway);
        }

        var final = new List<ContentElement>(blocks.Count);
        for (var i = 0; i < blocks.Count; i++)
        {
            ContentElement chosen = perClassifier[0][i];
            for (var c = 0; c < _classifiers.Length; c++)
            {
                if (perClassifier[c][i] is not ParagraphElement)
                {
                    chosen = perClassifier[c][i];
                    break;
                }
            }
            chosen.Id = startId++;
            final.Add(chosen);
        }
        return final;
    }
}
