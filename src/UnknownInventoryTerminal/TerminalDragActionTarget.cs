using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    internal sealed class TerminalDragActionTarget : MonoBehaviour
    {
        internal TerminalDragAction Action;
        internal Image Face = null!, Icon = null!;
        internal Ae2Image IconArt;

        internal void Draw(Ae2TerminalSkin skin, int scale, bool hovered, bool armed)
        {
            var face = Ae2ToolbarAppearance.Face(hovered && !armed, armed);
            var offset = Ae2ToolbarAppearance.OffsetY(face);
            var size = skin.SourceSize(face) ?? new Vector2Int(Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight - offset);
            NativeFlatInventoryPanel.Position(Face.rectTransform, new Rect(0, offset * scale, size.x * scale, size.y * scale));
            skin.Apply(Face, face, Ae2TerminalSkin.Slot);
            if (skin.SourceSize(IconArt) is { } iconSize && skin.VisibleBounds(IconArt) is { } visible)
            {
                var (x, y) = Ae2ToolbarAppearance.CenterIcon(visible, size.x, size.y, scale);
                NativeFlatInventoryPanel.Position(Icon.rectTransform, new Rect(x, y, iconSize.x * scale, iconSize.y * scale));
            }
        }
    }
}
