#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Linguini.Bundle;
using Linguini.Bundle.Types;
using Linguini.Shared.Types.Bundle;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Robust.Shared.Localization
{
    internal sealed partial class LocalizationManager
    {
        private void AddBuiltInFunctions(FluentBundle bundle)
        {
            AddCtxFunction(bundle, "ATTRIB", args => FuncAttrib(bundle, args));
        }

        private ILocValue FuncAttrib(FluentBundle bundle, LocArgs args)
        {
            if (args.Args.Count < 2)
                return new LocValueString("other");

            ILocValue entity0 = args.Args[0];
            if (entity0.Value != null)
            {
                EntityUid entity = (EntityUid)entity0.Value;
                ILocValue attrib0 = args.Args[1];
                if (TryGetEntityLocAttrib(entity, attrib0.Format(new LocContext(bundle)), out var attrib))
                {
                    return new LocValueString(attrib);
                }
            }

            return new LocValueString("other");
        }

        private void AddCtxFunction(FluentBundle ctx, string name, LocFunction function)
        {
            ctx.AddFunction(name, (args, options)
                => CallFunction(function, ctx, args, options), out _, InsertBehavior.Overriding);
        }

        private IFluentType CallFunction(
            LocFunction function,
            FluentBundle bundle,
            IList<IFluentType> positionalArgs,
            IDictionary<string, IFluentType> namedArgs)
        {
            var args = new ILocValue[positionalArgs.Count];
            for (var i = 0; i < args.Length; i++)
            {
                args[i] = positionalArgs[i].ToLocValue();
            }

            var options = new Dictionary<string, ILocValue>(namedArgs.Count);
            foreach (var (k, v) in namedArgs)
            {
                options.Add(k, v.ToLocValue());
            }

            var argStruct = new LocArgs(args, options);
            return function.Invoke(argStruct).FluentFromVal(new LocContext(bundle));
        }

        public void AddFunction(CultureInfo culture, string name, LocFunction function)
        {
            var bundle = _contexts[culture];

            bundle.AddFunction(name, (args, options)
                => CallFunction(function, bundle, args, options), out _, InsertBehavior.Overriding);
        }
    }

    internal sealed class FluentLocWrapperType : IFluentType
    {
        public readonly ILocValue WrappedValue;
        private readonly LocContext _context;

        public FluentLocWrapperType(ILocValue wrappedValue, LocContext context)
        {
            WrappedValue = wrappedValue;
            _context = context;
        }

        public string AsString()
        {
            return WrappedValue.Format(_context);
        }

        public IFluentType Copy()
        {
            return this;
        }
    }

    static class LinguiniAdapter
    {
        internal static ILocValue ToLocValue(this IFluentType arg)
        {
            return arg switch
            {
                FluentNone => new LocValueNone(""),
                FluentNumber number => new LocValueNumber(number),
                FluentString str => new LocValueString(str),
                FluentLocWrapperType value => value.WrappedValue,
                _ => throw new ArgumentOutOfRangeException(nameof(arg)),
            };
        }

        public static IFluentType FluentFromObject(this object obj, LocContext context)
        {
            return obj switch
            {
                ILocValue wrap => new FluentLocWrapperType(wrap, context),
                EntityUid entity => new FluentLocWrapperType(new LocValueEntity(entity), context),
                DateTime dateTime => new FluentLocWrapperType(new LocValueDateTime(dateTime), context),
                TimeSpan timeSpan => new FluentLocWrapperType(new LocValueTimeSpan(timeSpan), context),
                Color color => (FluentString)color.ToHex(),
                bool or Enum => (FluentString)obj.ToString()!.ToLowerInvariant(),
                string str => (FluentString)str,
                byte num => (FluentNumber)num,
                sbyte num => (FluentNumber)num,
                short num => (FluentNumber)num,
                ushort num => (FluentNumber)num,
                int num => (FluentNumber)num,
                uint num => (FluentNumber)num,
                long num => (FluentNumber)num,
                ulong num => (FluentNumber)num,
                double dbl => (FluentNumber)dbl,
                float dbl => (FluentNumber)dbl,
                _ => (FluentString)obj.ToString()!,
            };
        }

        public static IFluentType FluentFromVal(this ILocValue locValue, LocContext context)
        {
            return locValue switch
            {
                LocValueNone => FluentNone.None,
                LocValueNumber number => (FluentNumber)number.Value,
                LocValueString str => (FluentString)str.Value,
                _ => new FluentLocWrapperType(locValue, context),
            };
        }
    }
}
