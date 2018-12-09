using PoeHUD.Hud.Settings;
using SharpDX;

namespace Stashie.Filters
{
    public class FilterResult
    {
        public StashTabNode StashNode;
        public Vector2 ClickPos;

        public FilterResult(StashTabNode stashNode, ItemData itemData)
        {
            StashNode = stashNode;
            ClickPos = itemData.GetClickPos();
        }
    }
}