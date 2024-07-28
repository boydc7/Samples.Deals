using Rydr.Api.Dto.Helpers;

namespace Rydr.Api.Dto.Shared;

public class ValueWrap<T>
{
    public T Value { get; set; }
}

public class IntRange
{
    public IntRange() { }

    public IntRange(int min, int max)
    {
        Min = min;
        Max = max;
    }

    public int Min { get; set; }

    private int _max;

    public int Max
    {
        get => _max > 0
                   ? _max
                   : Min > 0
                       ? int.MaxValue
                       : _max;

        set => _max = value;
    }

    public static IntRange FromMin(int min) => new()
                                               {
                                                   Min = min > 0
                                                             ? min
                                                             : 0,
                                                   Max = int.MaxValue
                                               };

    public static IntRange FromMax(int max) => new()
                                               {
                                                   Min = 0,
                                                   Max = max > 0
                                                             ? max
                                                             : int.MaxValue
                                               };

    public static IntRange From(int min, int max) => new()
                                                     {
                                                         Min = min > 0
                                                                   ? min
                                                                   : 0,
                                                         Max = max > 0
                                                                   ? max
                                                                   : int.MaxValue
                                                     };

    public bool IsValid() => Min > 0 || _max > 0;
}

public class LongRange
{
    public LongRange() { }

    public LongRange(long min, long max)
    {
        Min = min;
        Max = max;
    }

    public long Min { get; set; }

    private long _max;

    public long Max
    {
        get => _max > 0
                   ? _max
                   : Min > 0
                       ? long.MaxValue
                       : _max;

        set => _max = value;
    }

    public static LongRange FromMin(long min) => new()
                                                 {
                                                     Min = min.Gz(0),
                                                     Max = long.MaxValue
                                                 };

    public static LongRange FromMax(long max) => new()
                                                 {
                                                     Min = 0,
                                                     Max = max.Gz(long.MaxValue)
                                                 };

    public static LongRange From(long min, long max) => new()
                                                        {
                                                            Min = min.Gz(0),
                                                            Max = max.Gz(long.MaxValue)
                                                        };

    public bool IsValid() => Min > 0 || _max > 0;
}

public class DoubleRange
{
    public DoubleRange() { }

    public DoubleRange(double min, double max)
    {
        Min = min;
        Max = max;
    }

    public double Min { get; set; }

    private double _max;

    public double Max
    {
        get => _max > 0
                   ? _max
                   : Min > 0
                       ? double.MaxValue
                       : _max;

        set => _max = value;
    }

    public static DoubleRange FromMin(double min) => new()
                                                     {
                                                         Min = min > 0
                                                                   ? min
                                                                   : 0,
                                                         Max = double.MaxValue
                                                     };

    public static DoubleRange FromMax(double max) => new()
                                                     {
                                                         Min = 0,
                                                         Max = max > 0
                                                                   ? max
                                                                   : double.MaxValue
                                                     };

    public static DoubleRange From(double min, double max) => new()
                                                              {
                                                                  Min = min > 0
                                                                            ? min
                                                                            : 0,
                                                                  Max = max > 0
                                                                            ? max
                                                                            : double.MaxValue
                                                              };

    public bool IsValid() => Min > 0 || _max > 0;
}
