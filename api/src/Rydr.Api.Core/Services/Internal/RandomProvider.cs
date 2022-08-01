using System;

namespace Rydr.Api.Core.Services.Internal
{
    public static class RandomProvider
    {
        private static readonly object _lockObject = new object();
        private static readonly Random _random = new Random(Guid.NewGuid().GetHashCode());

        public static int GetRandomInt()
        {
            lock(_lockObject)
            {
                return _random.Next();
            }
        }

        public static int GetRandomIntBeween(int min, int max)
        {
            lock(_lockObject)
            {
                return _random.Next(min, max + 1);
            }
        }

        public static double GetRandomDouble()
        {
            lock(_lockObject)
            {
                return _random.NextDouble();
            }
        }

        public static double GetRandomDoubleBetween(double min, double max)
        {
            lock(_lockObject)
            {
                return _random.NextDouble() * (max - min) + min;
            }
        }
    }
}
