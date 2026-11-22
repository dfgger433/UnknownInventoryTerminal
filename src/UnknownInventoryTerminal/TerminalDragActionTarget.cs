using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    internal sealed class TerminalDragActionTarget : MonoBehaviour
    {
        internal TerminalDragAction Action;
        internal Image Face = null!, Icon = null!;
        internal Ae2Image IconArt;

        internal string HintKey => Action == TerminalDragAction.Drain ? "inventory.drag.drain" : "inventory.drag.use";

        internal void Draw(Ae2TerminalSkin skin, int scale, bool hovered, bool armed) =>
            skin.ApplyToolbarFace(Face, Icon, Vector2.zero, IconArt, scale, hovered, armed);
    }
}
