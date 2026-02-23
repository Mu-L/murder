using Murder.Assets.Graphics;
using Murder.Core;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Murder.Assets;

public readonly struct TextIcon
{
    public readonly string Id = string.Empty;
    public readonly Portrait Portrait = new();

    public TextIcon() { }
}

public class TextIconsAsset : GameAsset
{
    /// <summary>
    /// This is a reference to all the icons that can be placed in-text.
    /// This can NOT be longer than 6 pixels wide. We take into account the length of 'M' character
    /// when outputting those in screen.
    /// </summary>
    public readonly ImmutableArray<TextIcon> Icons = [];

    public override char Icon => '\uf086';

    public override string EditorFolder => "#\uf518Story";

    private ImmutableDictionary<string, TextIcon>? _iconsCache = null;

    public TextIconsAsset() { }

    public Portrait? TryFetchIcon(string id)
    {
        if (_iconsCache is null)
        {
            InitializeCache();
        }

        if (_iconsCache.TryGetValue(id, out TextIcon icon))
        {
            return icon.Portrait;
        }

        return null;
    }

    [MemberNotNull(nameof(_iconsCache))]
    private void InitializeCache()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, TextIcon>(StringComparer.InvariantCultureIgnoreCase);
        foreach (TextIcon icon in Icons)
        {
            builder[icon.Id] = icon;
        }

        _iconsCache = builder.ToImmutable();
    }

    protected override void OnModified()
    {
        base.OnModified();

        _iconsCache = null;
    }
}