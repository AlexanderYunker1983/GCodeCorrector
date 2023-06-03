using MugenMvvmToolkit.Interfaces.Models;
using System.Windows.Controls;
using System.Windows;

namespace GCodeCorrector.Behaviors
{
    //Это workaround бага, при котором CanExecuteChanged не вызывается в контекстном меню при изменении параметра.
    //Чуть более подробно можно посмотреть здесь: http://stackoverflow.com/a/4892360
    public static class ContextMenuFixer
    {
        public static DependencyProperty CommandParameterProperty = DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(ContextMenuFixer),
            new PropertyMetadata(CommandParameter_Changed));

        private static void CommandParameter_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is MenuItem target)) return;

            target.CommandParameter = IsItemDisconnected(e.NewValue) ? null : e.NewValue;
            (target.Command as IRelayCommand)?.RaiseCanExecuteChanged();
        }

        private static bool IsItemDisconnected(object item)
        {
            if (item == null) return false;
            var isDisconnected = false;

            var itemType = item.GetType();
            if (itemType.FullName == null) return false;
            if (itemType.FullName.Equals("MS.Internal.NamedObject")) isDisconnected = true;

            return isDisconnected;
        }
        public static object GetCommandParameter(MenuItem target)
        {
            return target.GetValue(CommandParameterProperty);
        }

        public static void SetCommandParameter(MenuItem target, object value)
        {
            target.SetValue(CommandParameterProperty, value);
        }
    }
}