using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;

namespace AvaMovieMaker.Menus;

public static class MenuBindings
{
    public static CompiledBinding Of<TSource, TValue>(TSource source, string property, Func<TSource, TValue> get)
        where TSource : class =>
        Build(source, property, get, not: false);

    public static CompiledBinding Not<TSource>(TSource source, string property, Func<TSource, bool> get)
        where TSource : class =>
        Build(source, property, get, not: true);

    private static CompiledBinding Build<TSource, TValue>(TSource source, string property, Func<TSource, TValue> get, bool not)
        where TSource : class
    {
        var info = new ClrPropertyInfo(property, o => get((TSource)o), null, typeof(TValue));
        var builder = new CompiledBindingPathBuilder().Property(info, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor);
        if (not)
        {
            builder.Not();
        }

        return new CompiledBinding(builder.Build()) { Source = source, Mode = BindingMode.OneWay };
    }
}
