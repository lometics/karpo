// The shape of today's temperature line.
//
// The user: "the graph should be nicely interpolated with no sharp corners." Straight segments
// between the hours read as a folded ribbon; a curve reads as weather.
//
// The curve is a **monotone cubic Hermite** with Fritsch-Carlson tangents, not a plain Catmull-Rom.
// An ordinary spline overshoots around a turn, and an overshoot on a temperature graph is not a
// cosmetic wobble: a curve that dips below the coldest hour or rises above the warmest invents a
// peak the forecast does not contain, and someone reading it off the picture would be misled.
// Fritsch-Carlson limits each tangent so the curve between two hours stays between those two
// hours' own temperatures, which is the strongest promise this drawing can make.
//
// The tangents are worked out here and nowhere else, so the promise can be tested against awkward
// data (a spike, a plateau, a single hour, hours that are not an hour apart) without a window. The
// drawing turns each segment into the cubic Bézier that is exactly equal to its Hermite form, so
// the line is one smooth geometry at any zoom rather than a hundred little straight pieces.

namespace Karpo.Dock.Weather;

internal static class WeatherCurve
{
    /// <summary>
    /// The slope the curve leaves each point with, in value per unit of <paramref name="x"/>.
    /// <paramref name="x"/> must rise; both arrays are the same length.
    /// </summary>
    public static double[] Tangents(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        var count = Math.Min(x.Count, y.Count);
        var slopes = new double[count];
        if (count < 2)
        {
            return slopes;
        }

        // The straight slope of each gap, and a first guess at each point: the average of the two
        // gaps it sits between.
        var gaps = new double[count - 1];
        for (var index = 0; index < count - 1; index++)
        {
            var run = x[index + 1] - x[index];
            gaps[index] = run <= 0 ? 0 : (y[index + 1] - y[index]) / run;
        }
        slopes[0] = gaps[0];
        slopes[count - 1] = gaps[count - 2];
        for (var index = 1; index < count - 1; index++)
        {
            slopes[index] = (gaps[index - 1] + gaps[index]) / 2;
        }

        // Fritsch-Carlson: a flat gap pins both its ends flat, and no tangent may be more than
        // three times its gap's own slope. That is what keeps the curve inside the data.
        for (var index = 0; index < count - 1; index++)
        {
            if (gaps[index] == 0)
            {
                slopes[index] = 0;
                slopes[index + 1] = 0;
                continue;
            }
            var left = slopes[index] / gaps[index];
            var right = slopes[index + 1] / gaps[index];
            // A turn: the curve must not carry on past the point it turns at.
            if (left < 0)
            {
                slopes[index] = 0;
                left = 0;
            }
            if (right < 0)
            {
                slopes[index + 1] = 0;
                right = 0;
            }
            var reach = (left * left) + (right * right);
            if (reach > 9)
            {
                var pull = 3 / Math.Sqrt(reach);
                slopes[index] = pull * left * gaps[index];
                slopes[index + 1] = pull * right * gaps[index];
            }
        }
        return slopes;
    }

    /// <summary>
    /// The value of the curve at <paramref name="at"/>, for the tests: the same cubic the drawing
    /// hands to Direct2D, worked out the long way.
    /// </summary>
    public static double Value(IReadOnlyList<double> x, IReadOnlyList<double> y, IReadOnlyList<double> slopes, double at)
    {
        var count = Math.Min(x.Count, y.Count);
        if (count == 0)
        {
            return 0;
        }
        if (count == 1 || at <= x[0])
        {
            return y[0];
        }
        if (at >= x[count - 1])
        {
            return y[count - 1];
        }
        var index = 0;
        while (index + 2 < count && at >= x[index + 1])
        {
            index++;
        }
        var run = x[index + 1] - x[index];
        if (run <= 0)
        {
            return y[index];
        }
        var step = (at - x[index]) / run;
        var square = step * step;
        var cube = square * step;
        // Hermite's four blends.
        return ((2 * cube) - (3 * square) + 1) * y[index]
            + (cube - (2 * square) + step) * run * slopes[index]
            + ((-2 * cube) + (3 * square)) * y[index + 1]
            + (cube - square) * run * slopes[index + 1];
    }

    /// <summary>
    /// The two control points of the cubic Bézier equal to the Hermite segment from
    /// <paramref name="index"/> to the next point: the drawing adds these straight to a geometry.
    /// </summary>
    public static (double X1, double Y1, double X2, double Y2) Bezier(
        IReadOnlyList<double> x,
        IReadOnlyList<double> y,
        IReadOnlyList<double> slopes,
        int index)
    {
        var run = x[index + 1] - x[index];
        var third = run / 3;
        return (
            x[index] + third,
            y[index] + (slopes[index] * third),
            x[index + 1] - third,
            y[index + 1] - (slopes[index + 1] * third));
    }
}
