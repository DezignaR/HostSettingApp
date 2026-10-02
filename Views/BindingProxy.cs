// ==========================================
// ФАЙЛ 2: Views/BindingProxy.cs
// ==========================================
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace WpfApp1.Views
{
    public class BindingProxy : Freezable
    {
        protected override Freezable CreateInstanceCore() => new BindingProxy();

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new PropertyMetadata(null));

        public object Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }
    }
}