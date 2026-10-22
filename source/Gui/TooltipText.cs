using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VSMechanicalLocks.Gui;

internal static class TooltipText
{
    internal const string Tag = "vsmechanicallocksinfo";

    public static void Register() => VtmlUtil.TagConverters[Tag] = (api, token, fonts, onLink) =>
        new InfoComponent(api, token.ContentText, fonts.Peek());

    public static void Unregister() => VtmlUtil.TagConverters.Remove(Tag);

    sealed class InfoComponent : RichTextComponent
    {
        public InfoComponent(ICoreClientAPI api, string text, CairoFont font) : base(api, text, font)
        {
            // Word wrapping treats a long Chinese sentence as one oversized word.
            // Its split/explicit-newline path can insert empty lines and lose text.
            // Scope character wrapping to our own CJK descriptions.
            if (text.Any(c => c is >= '\u2e80' and <= '\u9fff' or >= '\uf900' and <= '\ufaff'))
                linebreak = EnumLinebreakBehavior.AfterCharacter;
        }
    }
}
