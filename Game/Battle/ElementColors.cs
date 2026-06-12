namespace DraconicWars.Game.Battle;

using System.Collections.Generic;
using DraconicWars.Sim.Units;
using Godot;

/// <summary>Single source for element accent colors across HUD cards, lane unit
/// tints, and popups — palette.json ramp anchors, never computed shades.</summary>
public static class ElementColors
{
    private static readonly Dictionary<Element, Color> Tints = new()
    {
        [Element.Fire] = Color.FromHtml("f9a875"),
        [Element.Storm] = Color.FromHtml("d3fc7e"),
        [Element.Venom] = Color.FromHtml("99e65f"),
        [Element.Stone] = Color.FromHtml("d1b187"),
        [Element.Frost] = Color.FromHtml("8fd3ff"),
    };

    public static Color Of(Element element)
    {
        return Tints.GetValueOrDefault(element, Colors.White);
    }
}
