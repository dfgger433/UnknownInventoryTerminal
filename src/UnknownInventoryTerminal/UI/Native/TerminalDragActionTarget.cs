using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    internal sealed class TerminalDragActionTarget : MonoBehaviour
    {
        internal TerminalDragAction Action;
        internal Image Face = null!, Icon = null!;
        internal Ae2Image IconArt;

        internal string HintKey => Action switch
        {
            TerminalDragAction.Drain => "inventory.drag.drain",
            TerminalDragAction.Pin => IconArt == Ae2Image.DragUnpin ? "inventory.drag.unpin" : "inventory.drag.pin",
            _ => "inventory.drag.use"
        };

        internal void Draw(Ae2TerminalSkin skin, int scale, bool hovered, bool armed) =>
            skin.ApplyToolbarFace(Face, Icon, Vector2.zero, IconArt, scale, hovered, armed);
    }
}
