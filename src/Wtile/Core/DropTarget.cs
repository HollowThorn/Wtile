using Wtile.Layouts;

namespace Wtile.Core;

public static class DropTarget
{
    public static long DistanceSquaredToRect(LayoutRect rect, int x, int y)
    {
        long dx = Math.Max(Math.Max(rect.X - x, 0), x - (rect.X + rect.Width - 1));
        long dy = Math.Max(Math.Max(rect.Y - y, 0), y - (rect.Y + rect.Height - 1));
        return dx * dx + dy * dy;
    }

    // The rect's diagonals split it into four triangles: right or bottom means after.
    public static bool DropsAfterTile(LayoutRect rect, int x, int y)
    {
        double dx = (x - (rect.X + rect.Width / 2.0)) / Math.Max(rect.Width, 1);
        double dy = (y - (rect.Y + rect.Height / 2.0)) / Math.Max(rect.Height, 1);
        return Math.Abs(dx) >= Math.Abs(dy) ? dx > 0 : dy > 0;
    }
}
