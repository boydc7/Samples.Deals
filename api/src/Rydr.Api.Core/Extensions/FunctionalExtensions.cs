using System;
using System.Threading.Tasks;

namespace Rydr.Api.Core.Extensions
{
    public static class FunctionalExtensions
    {
        public static Func<T, T> EchoIn<T>(this Action<T> voidAction)
            => t =>
               {
                   voidAction(t);

                   return t;
               };

        public static Func<T, Task<T>> EchoIn<T>(this Func<T, Task> voidAdyncAction)
            => async t =>
               {
                   await voidAdyncAction(t);

                   return t;
               };

        public static TTo Transform<TFrom, TTo>(this TFrom from, Func<TFrom, TTo> getter)
            => To(from, getter);

        public static TTo To<TFrom, TTo>(this TFrom from, Func<TFrom, TTo> getter)
            => getter(from);
    }
}
