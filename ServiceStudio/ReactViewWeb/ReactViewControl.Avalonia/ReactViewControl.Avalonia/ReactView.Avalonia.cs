using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using WebViewControl;

namespace ReactViewControl {

    partial class ReactView : BaseControl {

        partial void ExtraInitialize() {
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromLogicalTree += OnDetachedFromLogicalTree;
            VisualChildren.Add(View);
        }

        protected override void OnGotFocus(GotFocusEventArgs e) {
            e.Handled = true;
            View.Focus();
        }

        private void OnAttachedToVisualTree(object sender, Avalonia.VisualTreeAttachmentEventArgs e) {
            AttachedToVisualTree -= OnAttachedToVisualTree;
            TryLoadComponent(false);
        }

        internal static void AsyncExecuteInUI(Action action, bool lowPriority) {
            Dispatcher.UIThread.Post(action, lowPriority ? DispatcherPriority.Background : DispatcherPriority.Normal);
        }
        private void OnDetachedFromLogicalTree(object sender, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e) {
            InternalDispose();
        }

        protected override void InternalDispose() => Dispose();

        new public ContextMenu ContextMenu {
            set {
                base.ContextMenu = value;
                View.OpenContextMenu(value);
            }
            get => base.ContextMenu;
        }

    }
}
